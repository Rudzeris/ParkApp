# -*- coding: utf-8 -*-
"""
Проверка чтения рабочей книги машин без Visual Studio.

Повторяет разбор из ExcelCarRepository: поиск столбцов по названию, «отс.»
как отсутствие данных, два столбца «Марка автомобиля», строки без VIN,
дробная мощность, полис со сроком в одной ячейке, ссылка на лист
«Должностные лица».

Запуск:  python3 tools/cars-check.py <файл.xlsx> [лист]
"""

import re
import sys
import zipfile
import xml.etree.ElementTree as ET

M = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
R = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}'

ABSENT = ('отс.', 'отс', 'нет', '-', '—')
DATE_IN_TEXT = re.compile(r'(\d{1,2})[.,/](\d{1,2})[.,/](\d{2,4})')
POWER = re.compile(r'(\d+(?:[.,]\d+)?)\s*/\s*(\d+(?:[.,]\d+)?)')
SEPARATORS = re.compile(r'[,;/|\r\n]+')

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


def read_sheet(path, wanted=None):
    z = zipfile.ZipFile(path)

    shared = []
    if 'xl/sharedStrings.xml' in z.namelist():
        for si in ET.fromstring(z.read('xl/sharedStrings.xml')).iter(M + 'si'):
            shared.append(''.join(t.text or '' for t in si.iter(M + 't')))

    rels = dict((r.get('Id'), r.get('Target'))
                for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels')))

    part = None
    names = []
    for s in ET.fromstring(z.read('xl/workbook.xml')).iter(M + 'sheet'):
        names.append(s.get('name'))
        if part is None and (wanted is None or s.get('name') == wanted):
            target = rels[s.get(R + 'id')]
            part = target if target.startswith('xl/') else 'xl/' + target.lstrip('/')

    if part is None:
        sys.exit('нет листа %r; в книге: %s' % (wanted, names))

    rows = []
    for row in ET.fromstring(z.read(part)).iter(M + 'row'):
        line = {}
        for c in row.findall(M + 'c'):
            ve = c.find(M + 'v')
            if c.get('t') == 'inlineStr':
                inl = c.find(M + 'is')
                v = ''.join(x.text or '' for x in inl.iter(M + 't')) if inl is not None else None
            else:
                v = ve.text if ve is not None else None
                if c.get('t') == 's' and v is not None:
                    i = int(v)
                    v = shared[i] if 0 <= i < len(shared) else ''
            if v is not None:
                line[col_index(c.get('r'))] = v
        rows.append(line)

    while rows and not rows[0]:
        rows.pop(0)

    header = rows[0] if rows else {}
    width = (max(header) + 1) if header else 0
    headers = [header.get(i, '') for i in range(width)]
    return headers, rows[1:], names


def normalize(name):
    return re.sub(r'\s+', ' ', (name or '')).strip().lower()


def find(headers, *names):
    wanted = [normalize(n) for n in names]
    for i, h in enumerate(headers):
        if normalize(h) in wanted:
            return i
    for want in wanted:                       # затем по началу названия
        for i, h in enumerate(headers):
            if normalize(h).startswith(want):
                return i
    return -1


def find_all(headers, *names):
    wanted = [normalize(n) for n in names]
    return [i for i, h in enumerate(headers) if normalize(h) in wanted]


def text(row, column):
    if column < 0:
        return None
    value = row.get(column)
    if value is None:
        return None
    value = str(value).strip()
    if not value or value.lower() in ABSENT:
        return None
    return value


def reference(row, column):
    """Ссылка на пустую ячейку другого листа даёт в Excel ноль, а не пустоту."""
    value = text(row, column)
    return None if value == '0' else value


def power(value):
    if not value:
        return None, None
    m = POWER.search(value)
    if not m:
        return None, None
    return (round(float(m.group(1).replace(',', '.'))),
            round(float(m.group(2).replace(',', '.'))))


def insurance(value):
    if not value:
        return None, None
    m = DATE_IN_TEXT.search(value)
    if not m:
        return re.sub(r'\s+', ' ', value).strip(), None
    day, month, year = int(m.group(1)), int(m.group(2)), int(m.group(3))
    if year < 100:
        year += 2000
    rest = re.sub(r'\s+', ' ', (value[:m.start()] + value[m.end():])).strip()
    return (rest or None), '%02d.%02d.%d' % (day, month, year)


def load(path, sheet):
    headers, rows, names = read_sheet(path, sheet)

    models = find_all(headers, 'Марка автомобиля', 'Марка')
    model_col = models[0] if models else -1
    latin_col = models[1] if len(models) > 1 else -1
    vin_col = find(headers, 'VIN', 'ВИН')
    plate_col = find(headers, 'Гос. рег. знак', 'ГРЗ')
    power_col = find(headers, 'Мощность двигателя КВт/Л.С.', 'Мощность')
    policy_col = find(headers, 'Страховой полис', 'Полис')
    end_col = find(headers, 'Дата истечения страховки')
    official_id_col = find(headers, '№ Д л')
    official_col = find(headers, 'Должностное лицо')
    location_col = find(headers, 'Местонахождение')
    staff_col = find(headers, 'Штатная')

    cars = []
    for i, row in enumerate(rows):
        model = text(row, model_col)
        latin = text(row, latin_col)
        vin = text(row, vin_col)
        if model is None and latin is None and vin is None:
            continue

        plates = []
        raw = text(row, plate_col)
        if raw:
            plates = [p.strip() for p in SEPARATORS.split(raw) if p.strip()]

        kw, hp = power(text(row, power_col))
        policy, ends = insurance(text(row, policy_col))

        staff = text(row, staff_col)
        cars.append({
            'row': i + 1,
            'vin': vin,
            'key': vin or ('стр.%d' % (i + 1)),
            'model': model or latin,
            'latin': latin if model else None,
            'plates': plates,
            'kw': kw, 'hp': hp,
            'policy': policy, 'ends': ends or text(row, end_col),
            'officialId': text(row, official_id_col),
            'official': reference(row, official_col),
            'location': text(row, location_col),
            'staff': None if not staff else not ('вне' in staff.lower() or 'за штат' in staff.lower()),
        })

    return cars, headers, names


if __name__ == '__main__':
    path = sys.argv[1]
    sheet = sys.argv[2] if len(sys.argv) > 2 else 'Список всех машин'
    cars, headers, names = load(path, sheet)

    print('книга: листы %s' % names)
    print('лист «%s»: столбцов %d, машин прочитано %d' % (sheet, len(headers), len(cars)))
    print()
    for car in cars[:3] + cars[-2:]:
        print('  стр %3d  ключ=%-20s %-28s латиница=%-22s ГРЗ=%s' %
              (car['row'], car['key'], (car['model'] or '')[:28],
               (car['latin'] or '—')[:22], car['plates'] or '—'))
        if car['kw'] or car['policy'] or car['official']:
            print('           кВт/лс=%s/%s  полис=%r до %s  ДЛ=%s %s  место=%s' %
                  (car['kw'], car['hp'], car['policy'], car['ends'],
                   car['officialId'], car['official'], car['location']))
    print()
    if sheet == 'Список всех машин':
        check(len(cars) > 100, 'строки без VIN не потерялись: прочитано %d' % len(cars))
    check(not any(c['official'] == '0' for c in cars), 'ссылка на пустую ячейку не стала фамилией «0»')
    check(all(c['key'] for c in cars), 'у каждой машины есть ключ')
    check(len(set(c['key'] for c in cars)) == len(cars), 'ключи не повторяются')
    check(not any((c['model'] or '').lower() in ABSENT for c in cars), '«отс.» не попало в марку')
    check(not any('отс' in (p.lower()) for c in cars for p in c['plates']), '«отс.» не стало номером')
    withpower = [c for c in cars if c['kw']]
    check(len(withpower) == len([c for c in cars if c['kw'] or c['hp']]),
          'мощность разобрана у %d машин из %d' % (len(withpower), len(cars)))
    if sheet == 'Список всех машин':
        check(len(withpower) > 100, 'мощность разобрана почти у всех строк каталога')
        frac = [c for c in cars if c['kw'] == 103 and c['hp'] == 140]
        check(bool(frac), 'дробная мощность «102,7/139,7» округлена до 103/140')
    check(all(c['plates'] for c in cars) or sheet != 'Убраны',
          'у снятых машин номера разобраны, в том числе по два в ячейке')
    withpolicy = [c for c in cars if c['ends']]
    if find(headers, 'Страховой полис') >= 0:
        check(bool(withpolicy), 'срок страховки вытащен из ячейки полиса: %s'
              % (withpolicy[0]['ends'] if withpolicy else '—'))

    print()
    print('ИТОГ:', 'всё сошлось' if not FAILURES else 'ПРОВАЛЕНО %d' % len(FAILURES))
    sys.exit(1 if FAILURES else 0)
