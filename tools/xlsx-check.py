# -*- coding: utf-8 -*-
"""
Проверка чтения .xlsx без Visual Studio.

Здесь повторена логика XlsxReader.CellValue. Главное, что проверяется, —
ячейка с формулой-ссылкой на другой лист («=Люди!A$1»): в рабочих книгах
заказчика так заполнена графа «в чьё распоряжение». Excel хранит в такой
ячейке и формулу, и посчитанное значение; читать нужно значение.

Запуск:  python3 tools/xlsx-check.py
"""

import io
import os
import sys
import zipfile
import xml.etree.ElementTree as ET

MAIN = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
FAILURES = []


def check(condition, message):
    print(('  ok   ' if condition else '  ПЛОХО') + ' ' + message)
    if not condition:
        FAILURES.append(message)


def cell_value(cell, shared):
    """Порт XlsxReader.CellValue: (текст, это_строка, формула)."""
    kind = cell.get('t')

    formula_node = cell.find(MAIN + 'f')
    formula = formula_node.text if formula_node is not None else None

    if kind == 'inlineStr':
        inline = cell.find(MAIN + 'is')
        if inline is None:
            return None
        return (''.join(t.text or '' for t in inline.iter(MAIN + 't')), True, formula)

    value = cell.find(MAIN + 'v')
    if value is None:
        return None

    raw = value.text or ''

    if kind == 's':
        index = int(raw)
        return (shared[index], True, formula) if 0 <= index < len(shared) else None

    if kind == 'b':
        return ('ИСТИНА' if raw == '1' else 'ЛОЖЬ', True, formula)

    return (raw, kind == 'str' or kind == 'e', formula)


def escape(value):
    return (value.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;'))


def append_cell(reference, value, is_text, style, formula):
    """Порт XlsxWriter.AppendCell для чужой ячейки."""
    if not value and not formula:
        return ''

    out = '<c r="%s"' % reference
    if style:
        out += ' s="%d"' % style

    if formula:
        if is_text:
            out += ' t="str"'
        out += '><f>%s</f>' % escape(formula)
        if value:
            out += '<v>%s</v>' % escape(value)
        return out + '</c>'

    if is_text:
        return out + ' t="inlineStr"><is><t xml:space="preserve">%s</t></is></c>' % escape(value)

    return out + '><v>%s</v></c>' % value


def read(path, sheet_part='xl/worksheets/sheet1.xml'):
    with zipfile.ZipFile(path) as z:
        shared = []
        if 'xl/sharedStrings.xml' in z.namelist():
            root = ET.fromstring(z.read('xl/sharedStrings.xml'))
            for si in root.iter(MAIN + 'si'):
                shared.append(''.join(t.text or '' for t in si.iter(MAIN + 't')))

        sheet = ET.fromstring(z.read(sheet_part))

    rows = []
    for row in sheet.iter(MAIN + 'row'):
        rows.append([cell_value(c, shared) for c in row.findall(MAIN + 'c')])
    return rows


SHEET = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
<row r="1">
  <c r="A1" t="s"><v>0</v></c>
  <c r="B1" t="s"><v>1</v></c>
  <c r="C1" t="s"><v>2</v></c>
  <c r="D1" t="s"><v>3</v></c>
</row>
<row r="2">
  <c r="A2" t="s"><v>4</v></c>
  <c r="B2" t="inlineStr"><is><t>1234 АБ 34</t></is></c>
  <c r="C2" t="str"><f>Люди!A$1</f><v>начальник штаба подполковник Антонов А.А.</v></c>
  <c r="D2"><f>ЛИСТ!B$7</f><v>45891</v></c>
</row>
</sheetData></worksheet>'''

SHARED = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="5" uniqueCount="5">
<si><t>Марка автомобиля</t></si><si><t>Гос. рег. знак</t></si>
<si><t>Должностное лицо</t></si><si><t>Получили машину</t></si>
<si><t>КамАЗ-4310</t></si></sst>'''


def main():
    folder = os.path.join('tools', '.tmp')
    if not os.path.isdir(folder):
        os.makedirs(folder)

    path = os.path.join(folder, 'формулы.xlsx')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('xl/worksheets/sheet1.xml', SHEET)
        z.writestr('xl/sharedStrings.xml', SHARED)

    rows = read(path)
    header = [c[0] if c else None for c in rows[0]]
    data = [c[0] if c else None for c in rows[1]]
    kinds = [c[1] if c else None for c in rows[1]]

    print('шапка:', header)
    print('строка:', data)
    print()

    check(header[2] == 'Должностное лицо', 'заголовок из общих строк прочитан')
    check(data[0] == 'КамАЗ-4310', 'обычная строка прочитана')
    check(data[1] == '1234 АБ 34', 'встроенная строка прочитана')
    check(data[2] == 'начальник штаба подполковник Антонов А.А.',
          'ссылка на другой лист («=Люди!A$1») даёт значение, а не формулу')
    check(kinds[2] is True, 'результат формулы распознан как текст')
    check(data[3] == '45891', 'формула с числовым результатом даёт число (дата разбирается по стилю)')

    formulas = [c[2] if c else None for c in rows[1]]
    check(formulas[2] == 'Люди!A$1', 'формула запомнена, чтобы вернуть её при перезаписи')
    check(formulas[3] == 'ЛИСТ!B$7', 'числовая формула тоже запомнена')

    print()
    print('перезапись чужого столбца:')

    # так приложение возвращает на место ячейку, которой не понимает
    written = append_cell('C2', data[2], kinds[2], 0, formulas[2])
    print('  ', written)

    back = ET.fromstring(
        '<c xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"'
        + written[2:])
    value = cell_value(back, [])

    check('<f>Люди!A$1</f>' in written, 'после перезаписи в ячейке снова формула, а не её результат')
    check(value[0] == data[2], 'посчитанное значение тоже сохранено — таблица читается и без пересчёта')
    check(value[2] == 'Люди!A$1', 'перезаписанная ячейка читается обратно как формула')

    plain = append_cell('E2', 'Примечание от руки', True, 0, None)
    check('inlineStr' in plain, 'обычный текст по-прежнему пишется встроенной строкой')

    os.remove(path)
    os.rmdir(folder)

    print()
    print('ИТОГ:', 'всё сошлось' if not FAILURES else 'ПРОВАЛЕНО %d' % len(FAILURES))
    return 1 if FAILURES else 0


if __name__ == '__main__':
    sys.exit(main())
