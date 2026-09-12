# -*- coding: utf-8 -*-
"""
Проверка языка поиска без Visual Studio: повторяет SearchQuery.

  "текст"  — точное совпадение (поля целиком или отдельного слова);
  *текст*  — звёздочка это любые символы, в том числе ни одного;
  текст    — похожие слова: начало слова, кусок или опечатка.

Запуск:  python3 tools/search-check.py
"""

import re
import sys

SEPARATORS = re.compile(r'[ \t\n\r,;·/\\-]+')
CYRILLIC = 'АВЕКМНОРСТУХавекмнорстух'
LATIN = 'ABEKMHOPCTYXabekmhopctyx'

FAILURES = []


def check(condition, message):
    print(('  ok   ' if condition else '  ПЛОХО') + ' ' + message)
    if not condition:
        FAILURES.append(message)


def normalize(value):
    out = []
    for symbol in (value or '').lower():
        letter = 'е' if symbol == 'ё' else symbol
        i = CYRILLIC.find(letter)
        out.append(LATIN[i].lower() if i >= 0 else letter)
    return ''.join(out).strip()


def words(value):
    return [w for w in SEPARATORS.split(value) if w]


def distance(a, b):
    previous = list(range(len(b) + 1))
    for i in range(1, len(a) + 1):
        current = [i] + [0] * len(b)
        for j in range(1, len(b) + 1):
            cost = 0 if a[i - 1] == b[j - 1] else 1
            current[j] = min(current[j - 1] + 1, previous[j] + 1, previous[j - 1] + cost)
        previous = current
    return previous[len(b)]


def similar(value, word):
    if word in value:
        return True
    for candidate in words(value):
        if candidate.startswith(word):
            return True
        allowed = 2 if len(word) >= 6 else 1 if len(word) >= 4 else 0
        if not allowed:
            continue
        if abs(len(candidate) - len(word)) <= allowed \
                and distance(candidate, word) <= allowed:
            return True
        if len(candidate) > len(word) \
                and distance(candidate[:len(word)], word) <= allowed:
            return True
    return False


def matches(query, *fields):
    text = (query or '').strip()
    values = [normalize(f) for f in fields if f and f.strip()]

    if not text:
        return True
    if not values:
        return False

    if len(text) >= 2 and text[0] == '"' and text[-1] == '"':
        exact = normalize(text[1:-1].strip())
        if not exact:
            return True
        return any(v == exact for v in values)

    if '*' in text:
        pattern = '^' + '.*'.join(re.escape(normalize(p)) for p in text.split('*')) + '$'
        return any(re.search(pattern, v) for v in values)

    query_words = [normalize(w) for w in words(text) if normalize(w)]
    if not query_words:
        return True
    return all(any(similar(v, w) for v in values) for w in query_words)


CARS = [
    ('КамАЗ-4310', '1234 АБ 34', 'X1F53500J0000123'),
    ('КамАЗ-53501', '5678 ВГ 34', 'X1F53501K0000456'),
    ('УАЗ-3909', 'У 123 Б 12', 'XTT390900E0012345'),
    ('Toyota Fortuner', 'отс.', 'MR0HA3FS300051431'),
    ('мерседес-бенц', '222', ''),
    ('Lada Vesta', 'А 456 ВС 34', 'XTA219410M0123456'),
]


def find(query):
    return [c[0] for c in CARS if matches(query, *c)]


if __name__ == '__main__':
    print('машины:', ', '.join(c[0] for c in CARS))
    print()

    cases = [
        ('камаз', ['КамАЗ-4310', 'КамАЗ-53501'], 'слово находит обе машины марки'),
        ('камас', ['КамАЗ-4310', 'КамАЗ-53501'], 'опечатка в марке прощается'),
        ('мерс', ['мерседес-бенц'], 'начало слова достаточно'),
        ('камаз 43', ['КамАЗ-4310'], 'два слова сужают до одной машины'),
        ('1234', ['КамАЗ-4310', 'УАЗ-3909', 'Lada Vesta'],
         'кусок цифр ищется и в номере, и в VIN'),
        ('MR0HA3FS300051431', ['Toyota Fortuner'], 'VIN целиком'),
        ('0005143', ['Toyota Fortuner'], 'кусок VIN из середины'),
        ('"1234 АБ 34"', ['КамАЗ-4310'], 'в кавычках — точный номер'),
        ('"1234"', [], 'в кавычках кусок номера не подходит — нужен номер целиком'),
        ('"222"', ['мерседес-бенц'], 'в кавычках — точное совпадение поля целиком'),
        ('*123*', ['КамАЗ-4310', 'УАЗ-3909', 'Lada Vesta'], 'звёздочки с двух сторон — «где угодно»'),
        ('*456', ['КамАЗ-53501', 'Lada Vesta'],
         'звёздочка слева — «заканчивается на» (совпали VIN)'),
        ('камаз*', ['КамАЗ-4310', 'КамАЗ-53501'], 'звёздочка справа — «начинается с»'),
        ('', [c[0] for c in CARS], 'пустой запрос показывает весь список'),
        ('фыва', [], 'чужое слово не находит ничего'),
    ]

    for query, expected, label in cases:
        found = find(query)
        check(found == expected, '%-22s → %-46s %s'
              % (repr(query), ', '.join(found) or '—', label))

    print()
    check(find('А 456 ВС 34') == find('A 456 BC 34'),
          'номер находится в любой раскладке: кириллица и латиница равны')
    check(find('УАЗ') == find('уаз'), 'регистр не важен')

    print()
    print('ИТОГ:', 'всё сошлось' if not FAILURES else 'ПРОВАЛЕНО %d' % len(FAILURES))
    sys.exit(1 if FAILURES else 0)
