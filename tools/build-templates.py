# -*- coding: utf-8 -*-
"""
Собирает шаблоны ParkApp/Templates/*.docx из подлинных форм заказчика,
которые лежат в docs/requirements/forms.

Зачем скрипт, а не правка руками: в форме много подчёркнутых «линий»
(ctrl+U с пробелами и табуляциями) и текста, разрезанного Word'ом на
десятки «run»-ов. Правка вслепую ломает и то, и другое, а собранный
заново шаблон всегда можно сверить с оригиналом.

Главное правило: подстановка меняет ровно найденную подстроку внутри
своих run-ов, остальные run-ы вместе с их оформлением не трогаются.
Поэтому подчёркивание, табуляции и соседний текст остаются на месте.

Запуск:  python3 tools/build-templates.py
"""

import io
import os
import re
import shutil
import sys
import zipfile
import xml.etree.ElementTree as ET

W = '{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
XMLSPACE = '{http://www.w3.org/XML/1998/namespace}space'

FORMS = 'docs/requirements/forms'
TEMPLATES = 'ParkApp/Templates'


# --- разбор и сборка docx -------------------------------------------------

def read_document(path):
    """Возвращает (архив как словарь, корень document.xml)."""
    parts = {}
    with zipfile.ZipFile(path) as z:
        for name in z.namelist():
            parts[name] = z.read(name)

    raw = parts['word/document.xml'].decode('utf-8')
    for prefix, uri in re.findall(r'xmlns:([A-Za-z0-9]+)="([^"]+)"', raw):
        ET.register_namespace(prefix, uri)

    return parts, ET.fromstring(raw)


def write_document(parts, root, path):
    xml = ET.tostring(root, encoding='utf-8').decode('utf-8')
    xml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\r\n' + xml
    parts['word/document.xml'] = xml.encode('utf-8')

    if not os.path.isdir(os.path.dirname(path)):
        os.makedirs(os.path.dirname(path))

    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        for name, data in parts.items():
            z.writestr(name, data)


# --- подстановка с сохранением run-ов -------------------------------------

def splice(parts, start, length, replacement):
    """
    Меняет отрезок склеенного текста, не разрушая куски вокруг него.
    Значение попадает в первый задетый кусок — он несёт нужное оформление
    (подчёркивание, жирность), поэтому линия под подписью остаётся линией.
    """
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

    return placed


def paragraphs(root):
    return list(root.iter(W + 'p'))


def replace(paragraph, old, new):
    nodes = list(paragraph.iter(W + 't'))
    parts = [n.text or '' for n in nodes]

    index = ''.join(parts).find(old)
    if index < 0:
        return False

    splice(parts, index, len(old), new)

    for node, value in zip(nodes, parts):
        node.text = value
        node.set(XMLSPACE, 'preserve')

    return True


def edit(root, number, pairs):
    """pairs — список (что искать, на что менять) в одном абзаце."""
    paragraph = paragraphs(root)[number]
    for old, new in pairs:
        if not replace(paragraph, old, new):
            text = ''.join(n.text or '' for n in paragraph.iter(W + 't'))
            sys.exit('абзац %d: не найдено %r\n            текст: %r' % (number, old, text))


def text_of(paragraph):
    return ''.join(n.text or '' for n in paragraph.iter(W + 't'))


# --- путевой лист ---------------------------------------------------------

def build_waybill():
    parts, root = read_document(os.path.join(FORMS, 'Путевой лист.docx'))

    # разрешение сверху листа: блок целиком печатается не всегда
    edit(root, 0, [
        ('Использование', '{{#РАЗРЕШЕНИЕ}}{{РАЗРЕШЕНИЕ_ЗАГОЛОВОК}}'),
        (' машины вне наряда', ''),
    ])
    edit(root, 1, [
        (' с' + ' ' * 30 + 'до' + ' ' * 26 + 'разрешаю',
         ' с {{РАЗРЕШЕНИЕ_С|________________________}}'
         ' до {{РАЗРЕШЕНИЕ_ДО|________________________}} разрешаю'),
    ])
    edit(root, 2, [('Должность1', '{{РАЗРЕШИЛ_ДОЛЖНОСТЬ|__________________}}')])
    edit(root, 3, [
        ('Звание1', '{{РАЗРЕШИЛ_ЗВАНИЕ}}'),
        ('И. Фамилия', '{{РАЗРЕШИЛ_ФИО}}'),
    ])
    # порядок справа налево: иначе подчёркивания заполнителя попадут под поиск
    edit(root, 4, [
        ('2026', '{{РАЗРЕШЕНИЕ_ГОД|20__}}'),
        ('___________________', '{{РАЗРЕШЕНИЕ_МЕСЯЦ|___________________}}'),
        ('«___»', '«{{РАЗРЕШЕНИЕ_ДЕНЬ|___}}»'),
        ('г.', 'г.{{/РАЗРЕШЕНИЕ}}'),
    ])

    edit(root, 7, [('№_______', '№ {{НОМЕР|_______}}')])
    edit(root, 8, [
        ('20____', '{{ГОД|20____}}'),
        ('________________', '{{МЕСЯЦ|________________}} '),
        ('«______»', '«{{ДЕНЬ|______}}»'),
    ])

    edit(root, 10, [('Заполнять', '{{ЧАСТЬ}}')])

    # в этом абзаце справа стоит «Водитель и старший машины проинструктированы» —
    # его нельзя потерять, поэтому меняем только сами прочерки
    edit(root, 12, [
        ('20___', '{{ДЕЙСТВ_ГОД|20___}}'),
        ('_______________', '{{ДЕЙСТВ_МЕСЯЦ|_______________}}'),
        ('«_____»', '«{{ДЕЙСТВ_ДЕНЬ|_____}}»'),
    ])
    edit(root, 13, [
        ('Должность2', '{{ИНСТРУКТАЖ_ДОЛЖНОСТЬ}}'),
        ('звание2', '{{ИНСТРУКТАЖ_ЗВАНИЕ}}'),
        ('И. Фамилия', '{{ИНСТРУКТАЖ_ФИО}}'),
    ])

    # «Маршрут движения» — подпись формы, её оставляем; меняем только значение
    edit(root, 17, [('заполнять или пустой', '{{МАРШРУТ}}')])
    edit(root, 18, [
        ('Должность3', '{{ОТВЕТСТВЕННЫЙ_ДОЛЖНОСТЬ}}'),
        (' звание3', ' {{ОТВЕТСТВЕННЫЙ_ЗВАНИЕ}}'),
        ('И. Фамилия', '{{ОТВЕТСТВЕННЫЙ_ФИО}}'),
    ])

    # таблица «убытие / прибытие»
    edit(root, 57, [('Заполнять', '{{УБЫТИЕ_ДАТА}}')])
    edit(root, 60, [('Заполнять', '{{ПРИБЫТИЕ_ДАТА}}')])
    edit(root, 70, [('Заполнять', '{{УБЫТИЕ_ВРЕМЯ}}')])
    edit(root, 73, [('Заполнять', '{{ПРИБЫТИЕ_ВРЕМЯ}}')])

    # таблица «для каких целей назначается»
    edit(root, 106, [('Перевозка личного состава', '{{ЦЕЛЬ}}')])
    edit(root, 107, [('Заполнять', '{{МАРКА}}')])
    edit(root, 108, [('Заполнять', '{{ГРЗ}}')])
    edit(root, 111, [('тр.', '{{ГРУППА_ЭКСПЛУАТАЦИИ}}')])
    edit(root, 112, [('л/с или м/с ', '{{ГРУЗ}}')])

    write_document(parts, root, os.path.join(TEMPLATES, 'Путевой лист.docx'))


# --- наряд ----------------------------------------------------------------

def build_order():
    parts, root = read_document(os.path.join(FORMS, 'Наряд.docx'))

    edit(root, 1, [('ДолжностьХ', '{{СОГЛ_ДОЛЖНОСТЬ}}')])
    edit(root, 2, [('званиеХ', '{{СОГЛ_ЗВАНИЕ}}'), ('И. Фамилия', '{{СОГЛ_ФИО}}')])
    edit(root, 7, [('ДолжностьХ', '{{УТВ_ДОЛЖНОСТЬ}}')])
    edit(root, 8, [('званиеХ', '{{УТВ_ЗВАНИЕ}}'), ('И. Фамилия', '{{УТВ_ФИО}}')])

    edit(root, 12, [('234', '{{НОМЕР|______}}')])
    edit(root, 13, [('{текст}', '{{НАЗНАЧЕНИЕ}}')])
    edit(root, 14, [
        ('2026', '{{ГОД|20__}}'),
        ('августа', '{{МЕСЯЦ|_____________}}'),
        ('«22»', '«{{ДЕНЬ|___}}»'),
    ])

    edit(root, 163, [('Должность4', '{{ПОДПИСАНТ_ДОЛЖНОСТЬ}}')])
    edit(root, 164, [('звание4', '{{ПОДПИСАНТ_ЗВАНИЕ}}'), ('И. Фамилия', '{{ПОДПИСАНТ_ФИО}}')])

    build_order_rows(root)
    write_document(parts, root, os.path.join(TEMPLATES, 'Наряд.docx'))


def build_order_rows(root):
    """
    В образце заказчика десять машин двумя группами. Приложению нужны
    две строки-образца: заголовок группы и машина. Остальные удаляем.
    """
    rows = list(root.iter(W + 'tr'))

    def cells_of(row):
        return [re.sub(r'\s+', ' ', ''.join(n.text or '' for n in tc.iter(W + 't'))).strip()
                for tc in row.iter(W + 'tc')]

    group_rows = [r for r in rows
                  if len(cells_of(r)) == 1 and cells_of(r)[0].startswith('Наименование')]

    car_rows = [r for r in rows
                if len(cells_of(r)) == 10 and re.match(r'^(Марка\d+|Lada)', cells_of(r)[1])]

    if not group_rows or not car_rows:
        sys.exit('в образце наряда не нашлись строки групп и машин')

    group, car = group_rows[0], car_rows[0]

    for paragraph in group.iter(W + 'p'):
        if replace(paragraph, 'Наименование1', '{{ГРУППА}}'):
            break

    replacements = [
        ('Марка1', '{{МАРКА}}'),
        ('Номер_и_буквы регион', '{{ГРЗ}}'),
        ('тр.', '{{ГРУППА_ЭКСПЛУАТАЦИИ}}'),
        ('Цель1', '{{ЦЕЛЬ}}'),
        ('Маршрут1', '{{МАРШРУТ}}'),
        ('Должность1 звание1 Фамилия И.О.', '{{РАСПОРЯЖЕНИЕ}}'),
        ('Дата1', '{{ВЫЕЗД_ДАТА}}'),
        ('Время1', '{{ВЫЕЗД_ВРЕМЯ}}'),
        ('Дата2', '{{ВОЗВРАТ_ДАТА}}'),
        ('Время2', '{{ВОЗВРАТ_ВРЕМЯ}}'),
    ]

    for old, new in replacements:
        if not any(replace(p, old, new) for p in car.iter(W + 'p')):
            sys.exit('строка машины: не найдено %r' % old)

    # в образце крайние ячейки пустые: № п/п проставляет приложение,
    # примечание берётся из наряда
    cells = list(car.iter(W + 'tc'))
    fill_empty_cell(cells[0], '{{НОМЕР_СТРОКИ}}')
    fill_empty_cell(cells[-1], '{{ПРИМЕЧАНИЕ}}')

    for row in group_rows[1:] + car_rows[1:]:
        remove(root, row)


def fill_empty_cell(cell, value):
    for paragraph in cell.iter(W + 'p'):
        nodes = list(paragraph.iter(W + 't'))
        if nodes:
            nodes[0].text = value
            nodes[0].set(XMLSPACE, 'preserve')
        else:
            add_text(paragraph, value)
        return


def add_text(paragraph, value):
    run = ET.SubElement(paragraph, W + 'r')
    node = ET.SubElement(run, W + 't')
    node.text = value
    node.set(XMLSPACE, 'preserve')


def remove(root, element):
    for parent in root.iter():
        for child in list(parent):
            if child is element:
                parent.remove(child)
                return


# --- оборот ---------------------------------------------------------------

def build_waybill_back():
    """Оборот заполнять нечем: он копируется как есть."""
    shutil.copyfile(
        os.path.join(FORMS, 'Путевой лист (оборот).docx'),
        os.path.join(TEMPLATES, 'Путевой лист (оборот).docx'))


if __name__ == '__main__':
    build_waybill()
    build_order()
    build_waybill_back()
    print('шаблоны собраны в', TEMPLATES)
