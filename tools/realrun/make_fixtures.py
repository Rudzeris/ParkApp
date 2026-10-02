# -*- coding: utf-8 -*-
"""
Копии книг заказчика в том виде, в каком их пишет Excel: общие строки,
стили с текстовым форматом «@», формулы с посчитанным значением,
объединённые ячейки. Содержимое — по дампам настоящих файлов.
"""
import os, zipfile
from xml.sax.saxutils import escape

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fixtures')

CT = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
%s
<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
<Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
</Types>'''

# стили как в книге: 0 общий, 1 текст «@», 2 дата, 3 общий (сумма)
STYLES = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
<borders count="1"><border/></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="23">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyAlignment="1"><alignment wrapText="1"/></xf><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyAlignment="1"><alignment wrapText="1" vertical="top"/></xf>
</cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>'''


def col(i):
    s = ''
    i += 1
    while i:
        i, r = divmod(i - 1, 26)
        s = chr(65 + r) + s
    return s


class Book:
    def __init__(self):
        self.strings, self.index, self.sheets = [], {}, []

    def s(self, text):
        if text not in self.index:
            self.index[text] = len(self.strings)
            self.strings.append(text)
        return self.index[text]

    def sheet(self, name, rows, merges=()):
        """rows: список строк; ячейка — None, str, число или ('f', формула, значение, тип)."""
        out = []
        for r, row in enumerate(rows, 1):
            cells = []
            for c, v in enumerate(row):
                ref = '%s%d' % (col(c), r)
                if v is None:
                    continue
                if isinstance(v, tuple) and v[0] == 'f':
                    _, formula, value, kind = v
                    t = ' t="str"' if kind == 'str' else ''
                    cells.append('<c r="%s"%s><f>%s</f><v>%s</v></c>' % (ref, t, escape(formula), escape(str(value))))
                elif isinstance(v, tuple) and v[0] == 'date':
                    cells.append('<c r="%s" s="2"><v>%s</v></c>' % (ref, v[1]))
                elif isinstance(v, tuple) and v[0] == 's':
                    cells.append('<c r="%s" t="s" s="%d"><v>%d</v></c>' % (ref, v[2], self.s(v[1])))
                elif isinstance(v, str):
                    cells.append('<c r="%s" t="s" s="1"><v>%d</v></c>' % (ref, self.s(v)))
                else:
                    cells.append('<c r="%s" s="3"><v>%s</v></c>' % (ref, v))
            out.append('<row r="%d">%s</row>' % (r, ''.join(cells)))
        merge = ''
        if merges:
            merge = '<mergeCells count="%d">%s</mergeCells>' % (
                len(merges), ''.join('<mergeCell ref="%s"/>' % m for m in merges))
        xml = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
               '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
               '<sheetData>%s</sheetData>%s</worksheet>') % (''.join(out), merge)
        self.sheets.append((name, xml))

    def save(self, path):
        z = zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED)
        overrides = ''.join(
            '<Override PartName="/xl/worksheets/sheet%d.xml" ContentType="application/'
            'vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' % (i + 1)
            for i in range(len(self.sheets)))
        z.writestr('[Content_Types].xml', CT % overrides)
        z.writestr('_rels/.rels', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                   '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
                   '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/'
                   'relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>')
        sheets = ''.join('<sheet name="%s" sheetId="%d" r:id="rId%d"/>' % (escape(n), i + 1, i + 1)
                         for i, (n, _) in enumerate(self.sheets))
        z.writestr('xl/workbook.xml', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                   '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
                   'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
                   '<sheets>%s</sheets></workbook>' % sheets)
        rels = ''.join('<Relationship Id="rId%d" Type="http://schemas.openxmlformats.org/officeDocument/'
                       '2006/relationships/worksheet" Target="worksheets/sheet%d.xml"/>' % (i + 1, i + 1)
                       for i in range(len(self.sheets)))
        n = len(self.sheets)
        rels += ('<Relationship Id="rId%d" Type="http://schemas.openxmlformats.org/officeDocument/2006/'
                 'relationships/styles" Target="styles.xml"/>' % (n + 1))
        rels += ('<Relationship Id="rId%d" Type="http://schemas.openxmlformats.org/officeDocument/2006/'
                 'relationships/sharedStrings" Target="sharedStrings.xml"/>' % (n + 2))
        z.writestr('xl/_rels/workbook.xml.rels', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                   '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
                   '%s</Relationships>' % rels)
        for i, (_, xml) in enumerate(self.sheets):
            z.writestr('xl/worksheets/sheet%d.xml' % (i + 1), xml)
        z.writestr('xl/styles.xml', STYLES)
        sst = ''.join('<si><t xml:space="preserve">%s</t></si>' % escape(s) for s in self.strings)
        z.writestr('xl/sharedStrings.xml', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                   '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="%d" '
                   'uniqueCount="%d">%s</sst>' % (len(self.strings), len(self.strings), sst))
        z.close()


def fines():
    b = Book()
    head = ['№ п/п', 'Дата правонарушения, Ф.И.О. правонарушителя', 'Марка АТ', 'ГРЗ',
            'Дата привлечения', 'Номер постановления', 'Сумма штрафа', 'оплата, чек, дата', 'Примечание']
    rows = [
        head,
        [None, 'ОБЩАЯ СУММА', None, None, None, None, ('f', 'SUM(G3:G110)', 4500, 'n')],
        [1, 'Маслёнкин В.И. 29.12.2025', 'хавейл', '2уцуац', '11.01.2026', '435435', 2250,
         ('s', 'Оплата 19.02.2026 \nИД платежа 54354', 22), None, None, None, ('s', 'Пока не платить до уточнения', 21)],
        [2, 'Иванов П.Н.\n13.01.2026', 'хавейл', '2уцуац', '14.01.2026', '5435435', 750,
         'Оплата 19.02.2026 \nИД платежа 3453454', None, None, None, 'Либо платить, по старым, но делать приказ'],
        ['3', 'Крысов Ю.Н. \n06.03.2026', 'мерседес-бенц', '222', '12.03.2026', '3454353524', 1500,
         None, 'ов'],
        [4, 'Дубль Д.Д. 07.03.2026', 'УАЗ', '777', '13.03.2026', '435435', 999, None, 'вторая запись с тем же номером'],
    ]
    b.sheet('TEST', rows, merges=('L3:N3', 'L4:N4'))
    b.sheet('Сверка с ГИБДД', [['Месяц', 'Сумма'], ['январь', 3000]])
    b.save(os.path.join(OUT, 'Штрафы.xlsx'))


def cars():
    b = Book()
    head = ['Марка автомобиля', '№ п/п', 'Марка автомобиля', 'Гос. рег. знак', 'Год выпуска', 'VIN',
            'Модель и № двигателя', 'Мощность двигателя КВт/Л.С.', '№ шасси (рама)', '№ кузова', 'ПФМ',
            'ПТС', 'Свид. о регистрации', 'Страховой полис', 'Дата истечения страховки', 'Д/ карта',
            'Куда относится', 'Местонахождение', '№ Д л', 'Должностное лицо ', 'Штатная', 'Конфискат',
            'Вместимость (чел.)', 'Тип машины', 'Цвет', 'Объем двигателя (см3)', 'max m', 'm', 'Особые отметки']
    rows = [head,
        ['Skoda Kodiaq', None, 'Skoda Kodiaq', 'отс.', 2022, 'XW8LJ6NS5NH412345', 'DFG E06917', '110/150',
         'отс.', 'XW8LJ6NS5NH412345', 'ЦО № 012345', '164301042112345', 'отс.', 'ТТТ 123456789\n12.09.2027',
         ('f', 'IF(N2="отс.","отс.",IF(N2="","",DATEVALUE(RIGHT(N2,10))))', 46642, 'n'), 'отс.',
         'Гараж', 'ППД', 2, ('f', "'Должностные лица'!B$3", 'начальник штаба подполковник Антонов А.А.', 'str'),
         'За штатом', 'Конфискат', None, 'Легковой, Универсал', 'Бежевый', 1968, 2450, 1807,
         'ТС Оборудовано УВЭОС № 89701770000'],
        ['Тойота Фортунер', None, 'Toyota Fortuner', 'отс.', 2018, 'MR0HA3FS300051431', '1GD-FTV 4590952',
         '130/177', 'отс.', 'MR0HA3FS300051431', 'ЦО № 011243', 'отс.', 'отс.', 'отс.',
         ('f', 'IF(N3="отс.","отс.",IF(N3="","",DATEVALUE(RIGHT(N3,10))))', 'отс.', 'str'), 'отс.',
         None, 'СВО', None, ('f', "'Должностные лица'!B$2", 0, 'n'), None, None, None,
         'Легковой, Универсал', 'Серебристый', 2755, 2735, 2215, 'отс.'],
        ['Тойота Хайлюкс', None, 'Toyota Hilux', None, None, None, None, '130/177'],
        ['БМВ 520д', 1, 'BMW 520d', None, None, None, None, '135/184'],
        ['Лексус ЛХ570', 2, 'Lexus LX570', '0001 АВ 76 RUS\nС 486 УР 196 RUS', None, None, None, '102,7/139,7'],
    ]
    for i in range(3, 126):
        rows.append(['Модель %d' % i, i, 'Model %d' % i, None, None, None, None, '%d/%d' % (60 + i, 80 + i)])
    b.sheet('Список всех машин', rows)
    b.sheet('Убраны', [['№ п/п', 'Марка автомобиля', 'Гос. рег. знак', 'VIN'],
                       [1, 'AUDI Q8 HYBRID', '0001 АН 76 RUS', 'WAUZZZF13MD017548']])
    people = [['№', 'Должностное лицо ', 'Был'], [1, None, None],
              [2, 'начальник штаба подполковник Антонов А.А.', None]]
    for i in range(3, 45):
        people.append([i, None, None])
    b.sheet('Должностные лица', people)
    b.save(os.path.join(OUT, 'Машины.xlsx'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    fines()
    cars()
    print('готово:', sorted(os.listdir(OUT)))
