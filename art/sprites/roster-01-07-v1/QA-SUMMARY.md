# Итог проверки набора

Технический экспорт и Unity-выборка 128 кадров: PASS.

Художественные эвристики последних 24 наборов: 3 PASS, 15 WARN, 6 FAIL.

FAIL не скрыты:

- frames-v2-hex-cultist-walk-east: `leg_alternation`; value `["unclear", "far", "unclear", "far", "far", "near"]`; threshold `"lead differs between i and i+n/2"`.
- frames-v2-runescribe-cast-east: `identity_head_ratio`; value `{"0": 0.99, "1": 1.01, "2": 1.03, "3": 0.891, "4": 0.99, "5": 1.01}`; threshold `[0.95, 1.05]`.
- frames-v2-runescribe-cast-south: `identity_head_ratio`; value `{"0": 1.0, "1": 1.0, "2": 0.94, "3": 1.0, "4": 1.0, "5": 0.98}`; threshold `[0.95, 1.05]`.
- frames-v2-runescribe-cast-west: `identity_head_ratio`; value `{"0": 1.01, "1": 0.909, "2": 0.99, "3": 1.01, "4": 0.99, "5": 1.01}`; threshold `[0.95, 1.05]`.
- frames-v2-runescribe-idle-east: `identity_head_ratio`; value `{"0": 0.99, "1": 1.01, "2": 0.99, "3": 1.05}`; threshold `[0.95, 1.05]`.
- frames-v2-runescribe-walk-north: `identity_head_ratio`; value `{"0": 1.0, "1": 0.94, "2": 1.0, "3": 1.02, "4": 0.96, "5": 1.0}`; threshold `[0.95, 1.05]`.

Все WARN и FAIL, включая прошлые дубли, дословно сохранены в QA-DETAILS.md.
Фронтальная ходьба не проверяется эвристикой NEAR/FAR по оси X; её проекция другая.
Набор пригоден для импорта и визуальной проверки; это не утверждение о финальной художественной готовности.
