"""Reproducible summaries of actual Unity ECS probe results, not human success rates."""
import csv
import json
import statistics as st
from pathlib import Path

root = Path(__file__).resolve().parents[1] / 'art/playtests/boss-balance'
policies = ['Стоять', 'Двигаться по кругу', 'Реакция + дэш', 'Реакция без дэша']
bosses = ['Страж', 'Звонарь', 'Архивариус']
all_cases = []
for path in sorted(root.glob('iteration-*.json')):
    cases = json.loads(path.read_text(encoding='utf-8'))['cases']
    for case in cases:
        all_cases.append({'iteration': path.stem, **case})
with (root/'all-cases.csv').open('w',newline='',encoding='utf-8-sig') as stream:
    writer=csv.DictWriter(stream,fieldnames=all_cases[0].keys());writer.writeheader();writer.writerows(all_cases)
final=[c for c in all_cases if c['iteration']=='iteration-4-final']
lines=['# Измерения баланса боссов','',
       f'Всего сохранено **{len(all_cases)} боёв** за пять итераций. Финальная матрица: **{len(final)}**.',
       '', 'Это диагностические контроллеры, а не статистика человеческих игроков. В каждой попытке 100 HP, базовая скорость, обычный урон, без лечения и бессмертия. Оценка каждые 250 мс, применение решения через 150 мс; прогноз известных угроз на 1 с. Конец прогона — смерть, победа или 150 с.', '',
       '| Прокачка оружия | Стратегия | Победы | Медиана времени победы | Средний полученный урон |',
       '|---|---|---:|---:|---:|']
for rank in [0,2]:
    for policy,label in enumerate(policies):
        cases=[c for c in final if c['rank']==rank and c['policy']==policy];wins=[c for c in cases if c['victory']]
        median=f'{st.median(c["seconds"] for c in wins):.1f} с' if wins else '—'
        lines.append(f'| {rank} | {label} | {len(wins)}/{len(cases)} | {median} | {st.mean(c["damage"] for c in cases):.1f} |')
lines += ['', '## Реакция и дэш: по боссам', '', '| Босс | Прокачка | Победы | Время победы, медиана |', '|---|---:|---:|---:|']
for boss,label in enumerate(bosses):
    for rank in [0,2]:
        cases=[c for c in final if c['boss']==boss and c['rank']==rank and c['policy']==2];wins=[c for c in cases if c['victory']]
        median=f'{st.median(c["seconds"] for c in wins):.1f} с' if wins else '—'
        lines.append(f'| {label} | {rank} | {len(wins)}/{len(cases)} | {median} |')
lines += ['', '## Непройденные сценарии реактивного контроллера', '',
          'Поражение бота не доказывает отсутствие пути для человека; победа бота не доказывает комфортную читаемость.', '']
for c in final:
    if c['policy']==2 and not c['victory']:
        lines.append(f'- {bosses[c["boss"]]}, пара {c["pair"]}, ранг {c["rank"]}, seed {c["seed"]}, толпа {c["crowd"]}: {c["seconds"]:.1f} с, HP игрока {c["health"]:.1f}, HP босса {c["bossHealth"]:.0%}.')
lines += ['', 'Пары оружия: 0 — Runebolt + Ward; 1 — Runebolt + Orbit; 2 — Ward + Orbit.',
          'Seed 13/29/42 задаёт разные углы подхода, расстановку врагов и три начальных места, в том числе рядом с препятствиями. Толпа 0/24; дополнительные враги появляются по правилам кампании. Ранги 0/2 относятся к атаке, Vitality/FleetFoot/Magnet выключены для одинаковых HP/скорости. Уровни не повышаются, чтобы лечение при повышении не искажало сравнение.', '']
(root/'results.md').write_text('\n'.join(lines),encoding='utf-8')
print('\n'.join(lines[:18]))
