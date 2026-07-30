# Model Routing — Task Classification (MANDATORY, runs BEFORE any task)

> Source of truth for model selection. Referenced by CLAUDE.md, README_AI.md and master-agent.md.
> Claude cannot switch models itself — it must CLASSIFY the task, STATE the recommended model,
> and STOP if the current model is not cost-effective, so the user can switch first.

## Classification table

| Task type | Recommended model |
|---|---|
| Architecture / system design | **Opus / Fable** |
| Security review / pentest remediation | **Opus / Fable** |
| Complex debugging (multi-system, race conditions, data loss) | **Opus / Fable** |
| High-risk refactors (dedup logic, cache strategy, deploy pipeline) | **Opus / Fable** |
| Final review before release/demo | **Opus / Fable** |
| Normal implementation / feature slices | **Sonnet** |
| CRUD endpoints / DTOs / mappings | **Sonnet** |
| Tests (unit/integration) | **Sonnet** |
| Frontend work | **Sonnet** |
| EF migrations | **Sonnet** |
| Docs / README / comments | **Sonnet** |
| SonarQube issue fixes | **Sonnet** |

## Protocol (every task, first response)

1. Classify the task using the table above.
2. State one line: `Task class: <type> → Recommended model: <model>`.
3. If the CURRENT model is more expensive than recommended →
   **STOP and say:** "Recommended model: Sonnet. Please switch model before continuing."
4. If the task is high-risk and the current model is lighter than recommended →
   **STOP and say:** "Recommended model: Opus/Fable for architecture review."
5. Only proceed when the model matches, or the user explicitly says continue anyway.

## Session hygiene

- Keep sessions SHORT — one slice per session where possible.
- After every completed slice:
  1. Update `.claude/memory/shared-context.md` (what changed, decisions, open items).
  2. Commit/push ONLY if the user approved (standing rule: never push to dev unless told).
  3. Recommend closing the session.
