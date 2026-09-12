# -*- coding: utf-8 -*-
"""
Проверка чтения и перезаписи книги постановлений без Visual Studio.

Повторяет ExcelFineRepository: разбор ячейки «Ф.И.О. + дата», графы оплаты,
и — главное — сохранение того, что записью не является: строки «ОБЩАЯ СУММА»
с формулой над данными и примечаний, дописанных правее шапки.

Запуск:  python3 tools/fines-check.py <файл.xlsx> [лист]
"""

import re
import sys
import zipfile
import xml.etree.ElementTree as ET

M = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
R = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}'
DATE = re.compile(r'(\d{1,2})[.,/](\d{1,2})[.,/](\d{2,4})')

FAILURES = []


def check(condition, message):
    print(('  ok   ' if condition else '  ПЛОХО') + ' ' + message)
    if not condition:
        FAILURES.append(message)


def col_index(ref):
    letters = re.match(r'([A-Z]+)', ref).group(1)
    n = 0
    for ch in letters:
        n = n * 26 + (ord(ch) - 64)
    return n - 1


def read(path, wanted=None):
    z = zipfile.ZipFile(path)
    shared = []
    if 'xl/sharedStrings.xml' in z.namelist():
        for si in ET.fromstring(z.read('xl/sharedStrings.xml')).iter(M + 'si'):
            shared.append(''.join(t.text or '' for t in si.iter(M + 't')))

    rels = dict((r.get('Id'), r.get('Target'))
                for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels')))

    part, names = None, []
    for s in ET.fromstring(z.read('xl/workbook.xml')).iter(M + 'sheet'):
        names.append(s.get('name'))
        if part is None and (wanted is None or s.get('name') == wanted):
            target = rels[s.get(R + 'id')]
            part = target if target.startswith('xl/') else 'xl/' + target.lstrip('/')

    rows = []
    for row in ET.fromstring(z.read(part)).iter(M + 'row'):
        line = {}
        for c in row.findall(M + 'c'):
            f = c.find(M + 'f')
            ve = c.find(M + 'v')
            if c.get('t') == 'inlineStr':
                inl = c.find(M + 'is')
                v = ''.join(x.text or '' for x in inl.iter(M + 't')) if inl is not None else None
            else:
                v = ve.text if ve is not None else None
                if c.get('t') == 's' and v is not None:
                    i = int(v)
                    v = shared[i] if 0 <= i < len(shared) else ''
            formula = f.text if f is not None else None
            if v is not None or formula is not None:
                line[col_index(c.get('r'))] = (v, formula)
        rows.append(line)

    while rows and not rows[0]:
        rows.pop(0)

    header = rows[0] if rows else {}
    width = (max(header) + 1) if header else 0
    headers = [(header.get(i, ('', None))[0] or '') for i in range(width)]
    return headers, rows[1:], names


def normalize(name):
    return re.sub(r'\s+', ' ', (name or '')).strip().lower()


def find(headers, *names):
    wanted = [normalize(n) for n in names]
    for i, h in enumerate(headers):
        if normalize(h) in wanted:
            return i
    for want in wanted:
        for i, h in enumerate(headers):
            if normalize(h).startswith(want):
                return i
    return -1


def value(row, column):
    if column < 0:
        return None
    cell = row.get(column)
    if cell is None:
        return None
    v = (cell[0] or '').strip()
    return v or None


def split_offender(text):
    """«Иванов П.Н.⏎13.01.2026» — фамилия и дата в одной ячейке, порядок любой."""
    if not text:
        return None, None
    m = DATE.search(text)
    if not m:
        return re.sub(r'\s+', ' ', text).strip(), None
    day, month, year = int(m.group(1)), int(m.group(2)), int(m.group(3))
    if year < 100:
        year += 2000
    name = re.sub(r'\s+', ' ', (text[:m.start()] + text[m.end():])).strip(' ,;-')
    return (name or None), '%02d.%02d.%d' % (day, month, year)


def load(path, sheet):
    headers, rows, names = read(path, sheet)
    layout = {
        'row': find(headers, '№ п/п', '№'),
        'violation': find(headers, 'Дата правонарушения, Ф.И.О. правонарушителя',
                          'Дата правонарушения'),
        'brand': find(headers, 'Марка АТ', 'Марка'),
        'plate': find(headers, 'ГРЗ'),
        'resolution_date': find(headers, 'Дата привлечения'),
        'number': find(headers, 'Номер постановления'),
        'amount': find(headers, 'Сумма штрафа'),
        'payment': find(headers, 'оплата, чек, дата'),
        'notes': find(headers, 'Примечание'),
    }

    fines, foreign = [], []
    for i, row in enumerate(rows):
        number = value(row, layout['number'])
        if number is None:
            if row:
                foreign.append((len(fines), i, row))
            continue

        name, violated = split_offender(value(row, layout['violation']))
        fines.append({
            'row': i, 'number': number, 'name': name, 'violated': violated,
            'brand': value(row, layout['brand']), 'plate': value(row, layout['plate']),
            'attracted': value(row, layout['resolution_date']),
            'amount': value(row, layout['amount']),
            'payment': value(row, layout['payment']),
            'notes': value(row, layout['notes']),
            'extra': {k: v for k, v in row.items() if k >= len(headers)},
            'raw': row,
        })

    return headers, layout, fines, foreign


if __name__ == '__main__':
    path = sys.argv[1]
    sheet = sys.argv[2] if len(sys.argv) > 2 else None
    headers, layout, fines, foreign = load(path, sheet)

    print('шапка (%d столбцов): %s' % (len(headers), ' | '.join(headers)))
    print()
    print('строки, которые записями не являются: %d' % len(foreign))
    for after, i, row in foreign:
        cells = ', '.join('%s=%r%s' % (chr(65 + k), v[0], ' ⟵=' + v[1] if v[1] else '')
                          for k, v in sorted(row.items()))
        print('   после %d записей: %s' % (after, cells))
    print()
    print('постановления: %d' % len(fines))
    for f in fines:
        print('   № %-12s %-16s наруш. %-11s привлеч. %-11s %6s  %s' %
              (f['number'], f['name'] or '—', f['violated'] or '—',
               f['attracted'] or '—', f['amount'] or '—',
               (f['payment'] or '').replace('\n', ' / ')))
        if f['extra']:
            print('        правее шапки: %s' % {chr(65 + k): v[0] for k, v in f['extra'].items()})
    print()

    check(len(fines) == 3, 'прочитано записей: %d' % len(fines))
    check(len(foreign) == 1, 'строка «ОБЩАЯ СУММА» опознана как не-запись')
    check(any(v[1] and v[1].startswith('SUM') for _, _, r in foreign for v in r.values()),
          'её формула суммы запомнена, чтобы вернуть на место')
    check(all(f['name'] for f in fines), 'Ф.И.О. разобраны у всех записей')
    check(all(f['violated'] for f in fines), 'дата правонарушения вынута из той же ячейки')
    check(any('\n' in (f['payment'] or '') for f in fines),
          'графа оплаты с переносом строки прочитана целиком')
    wide = [f for f in fines if f['extra']]
    check(len(wide) == 2, 'примечания правее шапки найдены у %d записей' % len(wide))

    print()
    print('ИТОГ:', 'всё сошлось' if not FAILURES else 'ПРОВАЛЕНО %d' % len(FAILURES))
    sys.exit(1 if FAILURES else 0)
