# Mini Plan — ZLogger Integration & Application Logging (2026-10-09)

> **Status: executed 2026-10-09.** In Unity PlayMode, `LoggingValidator` passes 5 of 5 and the full suite passes 10 of 13. The 3 failures are the pre-existing GOAP-behaviour scenario failures, not logging. Deviation from the plan: per-sink floors are applied in `SafeSinkLogProcessor` (which also contains formatting failures) instead of in provider-specific filter rules.

ZLogger was approved by the producer on 2026-10-09. Goal: replace ad-hoc `Debug.Log` with levelled, per-channel, zero-cost-when-disabled logging, and instrument the simulation so behaviour such as "why didn't the crawlers finish the bounty?" can be answered from logs.

## Constraints
- **Zero allocation when disabled.** Use only ZLogger's interpolated-string API (`ZLogDebug($"...")`). The handler checks `IsEnabled` first, so hole expressions are not evaluated when the level is off. Never pre-build strings.
- **Thread-safe.** Cognition logs from thread-pool workers. Every line carries the simulation tick and an agent label, because arrival order is nondeterministic.
- **Read-only.** Logging never mutates simulation state, so determinism is unaffected.
- **Headless-safe.** Before `SimLog.Configure` runs (in validators), all loggers are `NullLogger`, which is silent and free.

## 1. Install (vendored, pinned)
- `Assets/Plugins/ZLogger/`: ZLogger 2.5.10 plus its netstandard2.1 dependency closure, resolved by `scripts/update-zlogger.sh` (`dotnet restore`). Excluded: facades Unity already ships (`System.Buffers`, `System.Memory`, `System.Numerics.Vectors`, `System.Threading.Tasks.Extensions`).
  - Why vendor instead of NuGetForUnity: restoring in headless batchmode isn't reliable, and vendoring is reproducible and reviewable. The script makes upgrades a single command.
- `Packages/manifest.json`: `com.cysharp.zlogger` (ZLogger.Unity) via git URL, pinned to tag `2.5.10`.
- `Assets/Scripts/csc.rsp`: `-langVersion:10`, needed for interpolated string handlers. `-nullable` is omitted, so the codebase isn't flooded with warnings.

## 2. `Simpiens.Diagnostics.SimLog` façade (`Assets/Scripts/Diagnostics/`)
- Static `ILogger` per channel: `Bootstrap`, `Simulation`, `Cognition`, `Planning`, `Economy`, `Supply`, `Navigation`, `Social`, `Groups`, `Drives`, `Intervention`. Category name is `Simpiens.<Channel>`.
- `SimLogSettings`:
  - global minimum level, plus per-channel overrides;
  - console sink (Unity console) at its own minimum level;
  - optional file sink (plain text) at its own level;
  - optional agent focus set.
- Configuration sources, later ones winning: defaults (Info to console) < `SIMPIENS_LOG` env var < `-simlog` / `-simlog-file` / `-simlog-focus` command-line args < code (`SimLog.Configure`).
- Line prefix: `[tick] LEVEL Channel |`. The tick comes from `SimLog.CurrentTick`, which `SimulationManager` sets each tick.
- Agent labels: `SimLog.RegisterAgentName(id, "Crawler2")` and `SimLog.Agent(id)`. Lookups use a ConcurrentDictionary; the label is computed only when the line is enabled.
- `SimLog.Traces(id)`: agent-focus filter for per-agent decision traces.
- Registered once in `AppBootstrapper`; flushed and disposed on shutdown.

## 3. Instrumentation (areas of use)
| Area | Channel | Level | What |
|---|---|---|---|
| Bootstrap/session | Bootstrap | Info | config, spawn summary, seeded stalls/bounties |
| Cognition tiers | Cognition | Debug | which tier produced the intent, reflex triggers, fallback used |
| Goal selection & planning | Planning | Debug / Trace | chosen goal + priority (Debug), all candidate priorities (Trace), plan found/failed + steps, plan advance/abort + reason |
| Intent outcomes | Cognition | Debug | intent result per agent (Success/Failed/PathBlocked…) |
| Harvest/contention | Social / Economy | Debug / Info | yields, depletion, dormancy, contention victory/defeat, yielding |
| Trade & bounties | Economy | Info / Debug | buy/sell, claim, deliver, scout report, every failure reason |
| Ledger | Economy | Debug / Error | escrow lock/release/refund, sinks/faucets, conservation violations |
| Supply | Supply | Info / Debug | caravan restocks/skips, regrowth |
| Bounty board & interventions | Intervention | Info / Warning | posted/rejected/cancelled/expired, price updates |
| Motor watchdog | Navigation | Debug | stall tiers 1–3, disengage; panic movement ignoring walls |
| Pathfinding | Navigation | Trace | unreachable targets |
| Gossip, hearth, groups | Social / Groups | Debug / Info | replaces the existing spammy `Debug.Log`s |
| Mental breaks/drives | Drives | Info | tantrum, starvation panic, exhaustion collapse |

All 28 existing `Debug.Log*` calls in non-Testing code move to `SimLog` at an appropriate level.

## 4. Verification
- New `LoggingValidator` (PlayMode `[Test]`):
  1. a disabled log allocates 0 bytes;
  2. channel overrides filter correctly;
  3. agent focus filters correctly;
  4. the file sink writes prefixed lines.
- `OutpostSupplyLoopScenario` enables Planning/Cognition/Economy at Debug for the crawlers only, writing to `Logs/outpost-supply-loop.log`.
- Scratch Roslyn build, then the full `scripts/run-playmode-tests.sh`.

## 5. Docs
ROADMAP §2.1 (approved stack + install status) and §1A (logging invariant); AGENTS.md (Diagnostics folder + logging rule); append to `architecture_history.md`.
