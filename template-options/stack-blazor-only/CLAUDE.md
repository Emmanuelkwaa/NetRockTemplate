# CLAUDE.md

NetRockTemplate - .NET 10 Blazor web app with static server-side rendering, orchestrated by Aspire, fully dockerized.

```
Browser → Blazor SSR (NetRockTemplate.UI, :8080 in containers)
Local orchestration: NetRockTemplate.AppHost (Aspire) + NetRockTemplate.ServiceDefaults
```

The UI started from the stock `dotnet new blazor` template with no interactivity and no authentication. There is no backend API in this project.

## Hard Rules

### Blazor

- Static SSR by default - add an interactive render mode only to the component that needs it, and say why in the PR
- `TimeProvider` (injected) - never `DateTime.UtcNow` or `DateTimeOffset.UtcNow`
- C# 13 `extension(T)` syntax for new extension methods
- Never `null!` - fix the design instead
- `/// <summary>` XML docs on all public and internal API surface
- `System.Text.Json` only - never `Newtonsoft.Json`
- Keep `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` in `Program.cs` - the container healthcheck uses `/health/live`

### Cross-Cutting

- Security first - when convenience and security conflict, choose security. Deny by default, open selectively. No PII in logs, URLs, or errors.
- Atomic commits: `type(scope): imperative description` (Conventional Commits). No `Co-Authored-By` lines.
- No dead code - remove unused imports, variables, functions, files, and stale references in the same commit
- No em dashes - never use `—` anywhere (code, comments, docs, UI). Use `-` or rewrite the sentence.

## Verification

Run before every commit. Fix all errors before committing. **Loop until green - never commit with failures.**

```bash
dotnet build src/backend/NetRockTemplate.slnx

# Aspire (run to verify local orchestration)
dotnet run --project src/backend/NetRockTemplate.AppHost
```

## Autonomous Behaviors

| Trigger | Action |
|---|---|
| **Any code change** | Run verification. Fix failures. Loop until green. |
| **Modifying existing files** | Check FILEMAP.md for downstream impact before editing. Update all affected files in the same commit. |
| **Logically complete unit of work** | Commit immediately with Conventional Commit message. Don't batch, don't ask. |
| **Build failure** | Read the error, fix it, re-run. Don't stop and report the error unless stuck after 3 attempts. |

## Agent Team

| Agent | Role | When to use |
|---|---|---|
| `filemap-checker` | Verifies downstream updates (read-only) | After modifying files with known consumers |
| `tech-writer` | Writes documentation | READMEs, session docs, guides |
| `product-owner` | Proposes prioritized work items (read-only) | Deciding what to work on next |

## File Roles

| File | Contains |
|---|---|
| `.claude/agents/` | Specialized agents for delegation |
| `.claude/skills/` | Step-by-step procedures (type `/` to list user-invocable skills) |
| `.claude/hooks/` | Lifecycle hooks: safety gates, auto-format, quality checks |
| `FILEMAP.md` | "When you change X, also update Y" - change impact tables |
