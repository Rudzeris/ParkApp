# -*- coding: utf-8 -*-
"""
Проверка шаблонов без Visual Studio.

Здесь повторена логика ParkApp/components/Infrastructure/Word/WordTemplate.cs:
подстановка с сохранением run-ов, заполнители {{КЛЮЧ|____}}, блоки
{{#ИМЯ}}…{{/ИМЯ}} и сборка многостраничного документа. Скрипт рисует
готовые документы и проверяет, что:

  * плейсхолдеров в результате не осталось;
  * пустые значения превратились в линии, а не в пустоту;
  * подчёркнутые линии формы уцелели;
  * текст формы никуда не пропал;
  * страниц столько, сколько машин.

Запуск:  python3 tools/render-check.py
"""

import re
import sys
import zipfile
import xml.etree.ElementTree as ET

W = '{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
XMLSPACE = '{http://www.w3.org/XML/1998/namespace}space'
PLACEHOLDER = re.compile(r'\{\{(?P<key>[^{}|]*?)(\|(?P<fill>[^{}]*))?\}\}')

TEMPLATES = 'ParkApp/Templates'
FAILURES = []


def check(condition, message):
    print(('  ok   ' if condition else '  ПЛОХО') + ' ' + message)
    if not condition:
        FAILURES.append(message)


# --- движок ---------------------------------------------------------------

def splice(parts, start, length, replacement):
    end = start + length
    offset = 0
    placed = False
    for i, part in enumerate(parts):
        part_start, part_end = offset, offset + len(part)
        offset = part_end
        if part_end <= start or part_start >= end:
            continue
        head = part[:max(start, part_start) - part_start]
        tail = part[min(end, part_end) - part_start:]
        parts[i] = (head + tail) if placed else (head + replacement + tail)
        placed = True


def fallback(value, match):
    if value and value.strip():
        return value
    return match.group('fill') if match.group('fill') is not None else ''


def replace_in_paragraph(paragraph, resolve):
    nodes = list(paragraph.iter(W + 't'))
    if not nodes:
        return
    parts = [n.text or '' for n in nodes]
    if '{{' not in ''.join(parts):
        return

    changed, start = False, 0
    while True:
        whole = ''.join(parts)
        if start > len(whole):
            break
        match = PLACEHOLDER.search(whole, start)
        if not match:
            break
        value = resolve(match)
        if value is None:
            start = match.end()
            continue
        splice(parts, match.start(), match.end() - match.start(), value)
        changed, start = True, match.start() + len(value)

    if changed:
        for node, value in zip(nodes, parts):
            node.text = value
            node.set(XMLSPACE, 'preserve')


def fill_paragraph(paragraph, values):
    if not values:
        return
    replace_in_paragraph(
        paragraph,
        lambda m: None if m.group('key') not in values else fallback(values[m.group('key')], m))


def strip_placeholders(root):
    for paragraph in root.iter(W + 'p'):
        replace_in_paragraph(paragraph, lambda m: fallback('', m))


def remove_marker(paragraph, marker):
    nodes = list(paragraph.iter(W + 't'))
    parts = [n.text or '' for n in nodes]
    index = ''.join(parts).find(marker)
    if index < 0:
        return
    splice(parts, index, len(marker), '')
    for node, value in zip(nodes, parts):
        node.text = value
        node.set(XMLSPACE, 'preserve')


def paragraph_text(p):
    return ''.join(n.text or '' for n in p.iter(W + 't'))


def keep_block_in(elements, name, keep, parents):
    open_tag, close_tag = '{{#%s}}' % name, '{{/%s}}' % name
    paragraphs = [p for e in elements for p in ([e] if e.tag == W + 'p' else []) + list(e.iter(W + 'p'))
                  if p.tag == W + 'p']
    seen, ordered = set(), []
    for p in paragraphs:
        if id(p) not in seen:
            seen.add(id(p))
            ordered.append(p)

    start = next((i for i, p in enumerate(ordered) if open_tag in paragraph_text(p)), -1)
    if start < 0:
        return
    end = next((i for i in range(start, len(ordered)) if close_tag in paragraph_text(ordered[i])), start)

    if keep:
        for p in ordered[start:end + 1]:
            remove_marker(p, open_tag)
            remove_marker(p, close_tag)
        return

    for p in ordered[start:end + 1]:
        parents[id(p)].remove(p)


def build_parents(root):
    parents = {}
    for parent in root.iter():
        for child in parent:
            parents[id(child)] = parent
    return parents


def split_pages(body):
    pages, current = [], []
    for element in list(body):
        if element.tag == W + 'p' and any(
                br.get(W + 'type') == 'page' for br in element.iter(W + 'br')):
            pages.append(current)
            current = []
            continue
        if element.tag == W + 'sectPr':
            continue
        current.append(element)
    pages.append(current)
    return pages


def open_template(name):
    parts = {}
    with zipfile.ZipFile('%s/%s' % (TEMPLATES, name)) as z:
        for entry in z.namelist():
            parts[entry] = z.read(entry)
    raw = parts['word/document.xml'].decode('utf-8')
    for prefix, uri in re.findall(r'xmlns:([A-Za-z0-9]+)="([^"]+)"', raw):
        ET.register_namespace(prefix, uri)
    return ET.fromstring(raw)


def save_pages(name, pages):
    root = open_template(name)
    body = root.find(W + 'body')

    sect = body.findall(W + 'sectPr')
    sect = sect[-1] if sect else None
    template = [e for e in list(body) if e is not sect]
    for e in template:
        body.remove(e)

    def add(element):
        if sect is not None:
            body.insert(list(body).index(sect), element)
        else:
            body.append(element)

    import copy
    for i in range(len(pages)):
        if i:
            br = ET.Element(W + 'p')
            r = ET.SubElement(br, W + 'r')
            ET.SubElement(r, W + 'br').set(W + 'type', 'page')
            add(br)
        for e in template:
            add(copy.deepcopy(e))

    parents = build_parents(root)
    for page, content in zip(split_pages(body), pages):
        for block, keep in content.get('blocks', {}).items():
            keep_block_in(page, block, keep, parents)
        for element in page:
            if id(element) in parents and parents[id(element)] is None:
                continue
            targets = [element] if element.tag == W + 'p' else []
            targets += list(element.iter(W + 'p'))
            for p in targets:
                fill_paragraph(p, content['values'])

    strip_placeholders(root)
    return root


# --- отрисовка для глаз ---------------------------------------------------

def lines_of(elements):
    out = []
    for element in elements:
        for p in element.iter(W + 'p'):
            text = ''
            for r in p.findall(W + 'r'):
                if r.find(W + 'tab') is not None:
                    text += '\t'
                text += ''.join(n.text or '' for n in r.findall(W + 't'))
            out.append(text)
    return out


def pages_of(root):
    """Готовый документ, разложенный по страницам: их разделяют разрывы."""
    return [lines_of(page) for page in split_pages(root.find(W + 'body'))]


def underlined_text(root):
    """Текст, лежащий в подчёркнутых run-ах: это и есть линии для подписей."""
    found = []
    for r in root.iter(W + 'r'):
        rpr = r.find(W + 'rPr')
        if rpr is not None and rpr.find(W + 'u') is not None:
            found.append(''.join(n.text or '' for n in r.findall(W + 't')))
    return found


def main():
    date, back = '22.08.26', '23.08.26'

    car = {
        'НОМЕР': '', 'ДЕНЬ': '22', 'МЕСЯЦ': 'августа', 'ГОД': '2026',
        'ЧАСТЬ': 'войсковая часть 01234',
        'ДЕЙСТВ_ДЕНЬ': '23', 'ДЕЙСТВ_МЕСЯЦ': 'августа', 'ДЕЙСТВ_ГОД': '2026',
        'ИНСТРУКТАЖ_ДОЛЖНОСТЬ': 'начальник автослужбы', 'ИНСТРУКТАЖ_ЗВАНИЕ': 'майор',
        'ИНСТРУКТАЖ_ФИО': 'И. Иванов',
        'ОТВЕТСТВЕННЫЙ_ДОЛЖНОСТЬ': 'зам. командира', 'ОТВЕТСТВЕННЫЙ_ЗВАНИЕ': 'подполковник',
        'ОТВЕТСТВЕННЫЙ_ФИО': 'С. Сидоров',
        'МАРШРУТ': 'ППД — полигон — ППД',
        'УБЫТИЕ_ДАТА': date, 'УБЫТИЕ_ВРЕМЯ': '06:00',
        'ПРИБЫТИЕ_ДАТА': back, 'ПРИБЫТИЕ_ВРЕМЯ': '06:00',
        'ЦЕЛЬ': 'Перевозка личного состава', 'МАРКА': 'КамАЗ-4310', 'ГРЗ': '1234 АБ 34',
        'ГРУППА_ЭКСПЛУАТАЦИИ': 'тр.', 'ГРУЗ': 'л/с',
        'РАЗРЕШЕНИЕ_ЗАГОЛОВОК': 'Использование машины',
        'РАЗРЕШЕНИЕ_С': '22.08.26 06:00', 'РАЗРЕШЕНИЕ_ДО': '23.08.26 06:00',
        'РАЗРЕШЕНИЕ_ДЕНЬ': '22', 'РАЗРЕШЕНИЕ_МЕСЯЦ': 'августа', 'РАЗРЕШЕНИЕ_ГОД': '2026',
        'РАЗРЕШИЛ_ДОЛЖНОСТЬ': 'командир части', 'РАЗРЕШИЛ_ЗВАНИЕ': 'полковник',
        'РАЗРЕШИЛ_ФИО': 'П. Петров',
    }

    # вторая машина — реквизиты не заполнены: проверяем, что остаются линии
    blank = dict((k, '') for k in car)
    blank['МАРКА'] = 'УАЗ-3909'
    blank['ГРЗ'] = 'У 123 Б 12'

    pages = [
        {'values': car, 'blocks': {'РАЗРЕШЕНИЕ': True}},
        {'values': blank, 'blocks': {'РАЗРЕШЕНИЕ': False}},
    ]

    root = save_pages('Путевой лист.docx', pages)
    sheets = pages_of(root)
    whole = '\n'.join('\n'.join(s) for s in sheets)

    print('=== путевой лист 1 — машина в наряде, реквизиты заполнены')
    for line in sheets[0][:20]:
        print('   ', line.rstrip())

    print()
    print('=== путевой лист 2 — вне наряда, реквизиты не заполнены')
    for line in sheets[1][:14]:
        print('   ', line.rstrip())

    print()
    print('=== проверки')
    check(len(sheets) == 2, 'страниц по числу машин: %d' % len(sheets))
    check('{{' not in whole and '}}' not in whole, 'в документе не осталось плейсхолдеров')

    first, second = '\n'.join(sheets[0]), '\n'.join(sheets[1])

    check('Водитель и старший машины проинструктированы' in first,
          'текст формы «Водитель и старший машины проинструктированы» на месте')
    check('Маршрут движения ППД' in first, 'подпись «Маршрут движения» осталась перед значением')
    check('войсковая часть 01234' in first, 'наименование части подставлено')
    check('И. Иванов' in first and 'С. Сидоров' in first, 'подписанты подставлены')
    check('Водитель________________________________________________' in first,
          'линия для подписи водителя цела')
    check('КамАЗ-4310' in first and '1234 АБ 34' in first, 'машина первого листа подставлена')

    marks = underlined_text(root)
    check(any('войсковая часть' in m for m in marks),
          'наименование части напечатано на подчёркнутой линии')
    check(any('И. Иванов' in m for m in marks),
          'фамилия инструктировавшего напечатана на подчёркнутой линии')

    check('разрешаю' in first, 'на машине в наряде блок разрешения напечатан')
    check('Использование машины' in first and 'вне наряда' not in first,
          'формулировка разрешения — «Использование машины»')

    check('разрешаю' not in second, 'на второй машине блок разрешения убран целиком')
    check('УАЗ-3909' in second, 'вторая машина подставлена')
    check('ПУТЕВОЙ ЛИСТ № _______' in second, 'у незаполненного номера осталась линия')
    check('«_____»' in second and '20___' in second,
          'у незаполненной даты действия остались прочерки')
    check('(организации)___' in second and 'войсковая часть' not in second,
          'незаполненное наименование части не подтянуло чужое значение')

    print()
    print('=== наряд')
    check(check_order(), 'наряд собран по числу машин')

    print()
    print('ИТОГ:', 'всё сошлось' if not FAILURES else 'ПРОВАЛЕНО %d проверок' % len(FAILURES))
    return 1 if FAILURES else 0


def check_order():
    """Наряд: строки размножаются по числу машин, группы идут заголовками."""
    import copy

    root = open_template('Наряд.docx')

    group_row = next(tr for tr in root.iter(W + 'tr')
                     if '{{ГРУППА}}' in ''.join(n.text or '' for n in tr.iter(W + 't')))
    car_row = next(tr for tr in root.iter(W + 'tr')
                   if '{{МАРКА}}' in ''.join(n.text or '' for n in tr.iter(W + 't')))

    rows = [
        (True, {'ГРУППА': 'Группа боевых машин'}),
        (False, {'НОМЕР_СТРОКИ': '1', 'МАРКА': 'КамАЗ-4310', 'ГРЗ': '1234 АБ 34',
                 'ГРУППА_ЭКСПЛУАТАЦИИ': 'тр.', 'ЦЕЛЬ': 'Перевозка личного состава',
                 'МАРШРУТ': 'ППД — полигон — ППД', 'РАСПОРЯЖЕНИЕ': 'нач. штаба',
                 'ВЫЕЗД_ДАТА': '22.08.26', 'ВЫЕЗД_ВРЕМЯ': '06:00',
                 'ВОЗВРАТ_ДАТА': '23.08.26', 'ВОЗВРАТ_ВРЕМЯ': '06:00', 'ПРИМЕЧАНИЕ': ''}),
        (False, {'НОМЕР_СТРОКИ': '2', 'МАРКА': 'УАЗ-3909', 'ГРЗ': 'У 123 Б 12',
                 'ГРУППА_ЭКСПЛУАТАЦИИ': 'тр.', 'ЦЕЛЬ': 'Обеспечение',
                 'МАРШРУТ': 'ППД — город', 'РАСПОРЯЖЕНИЕ': '',
                 'ВЫЕЗД_ДАТА': '22.08.26', 'ВЫЕЗД_ВРЕМЯ': '07:00',
                 'ВОЗВРАТ_ДАТА': '22.08.26', 'ВОЗВРАТ_ВРЕМЯ': '22:00', 'ПРИМЕЧАНИЕ': ''}),
    ]

    parents = build_parents(root)
    table = parents[id(car_row)]
    anchor = car_row

    for is_group, values in rows:
        source = group_row if is_group else car_row
        clone = copy.deepcopy(source)
        for p in clone.iter(W + 'p'):
            fill_paragraph(p, values)
        table.insert(list(table).index(anchor) + 1, clone)
        anchor = clone

    table.remove(car_row)
    table.remove(group_row)

    fill = {'НОМЕР': '234', 'НАЗНАЧЕНИЕ': 'на 22 августа',
            'ДЕНЬ': '22', 'МЕСЯЦ': 'августа', 'ГОД': '2026',
            'СОГЛ_ДОЛЖНОСТЬ': 'нач. штаба', 'СОГЛ_ЗВАНИЕ': 'подполковник', 'СОГЛ_ФИО': 'А. Антонов',
            'УТВ_ДОЛЖНОСТЬ': 'командир части', 'УТВ_ЗВАНИЕ': 'полковник', 'УТВ_ФИО': 'П. Петров',
            'ПОДПИСАНТ_ДОЛЖНОСТЬ': 'нач. автослужбы', 'ПОДПИСАНТ_ЗВАНИЕ': 'майор',
            'ПОДПИСАНТ_ФИО': 'И. Иванов'}
    for p in root.iter(W + 'p'):
        fill_paragraph(p, fill)
    strip_placeholders(root)

    text = '\n'.join(lines_of(list(root.find(W + 'body'))))
    for line in text.split('\n')[:6]:
        print('   ', line.rstrip())

    body_rows = [tr for tr in root.iter(W + 'tr')]
    ok = ('{{' not in text
          and 'Группа боевых машин' in text
          and 'КамАЗ-4310' in text and 'УАЗ-3909' in text
          and 'А. Антонов' in text and 'П. Петров' in text and 'И. Иванов' in text
          and 'Марка2' not in text and 'Цель1' not in text)

    marks = underlined_text(root)
    ok = ok and any('А. Антонов' in m for m in marks) and any('П. Петров' in m for m in marks)
    print('    строк в таблице: %d' % len(body_rows))
    return ok


if __name__ == '__main__':
    sys.exit(main())
