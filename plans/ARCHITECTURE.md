# Architecture Layers - Phase 1

## Stack Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│  Managers (Phase 2+)                                            │
│  - PlaylistManager, QuickMediaManager, ButtonInputManager, etc  │
│  - Own configuration, state, persistence                        │
│  - Called by Web UI                                             │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  Handlers (Phase 2+)                                            │
│  - PlaylistHandler, QuickMediaHandler, ButtonInputHandler, etc  │
│  - Run at runtime, respond to events                            │
│  - Request/release matrix control                               │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  IMatrixController (Phase 1 ✓)                                  │
│  - RequestControlAsync(IHandler, CancellationToken)             │
│  - ReleaseControl(IHandler)                                     │
│  - Only 1 handler has access at a time                          │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  IMatrixDisplay (Phase 1 ✓)                                     │
│  - WithMatrix<T>(Func<RGBLedMatrix, RGBLedCanvas, T>)           │
│  - WithMatrix(Action<RGBLedMatrix, RGBLedCanvas>)               │
│  - Synchronized callback-based access                           │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  MatrixService (existing, unchanged)                            │
│  - Owns RGBLedMatrix lifecycle                                  │
│  - Handles matrix reinitialization on config changes            │
│  - Thread-safe via internal lock                                │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  rpi-rgb-led-matrix (submodule)                                 │
│  - Hardware matrix control                                      │
└─────────────────────────────────────────────────────────────────┘
```

## Control Flow: Handler Gains Matrix Access

```
Handler A (running)                Handler B (waiting)
        │                                   │
        │ Has matrix control                │
        │                                   │
        │                           RequestControlAsync
        │                                   │
        │◄──────────── SemaphoreSlim ──────┤ (queues here)
        │
        │ ... does work ...
        │
        ReleaseControl
        │
        ├─► SemaphoreSlim.Release()
        │
        │   ┌─► Handler B wakes up
        │   │
        └─► CurrentController = Handler B
            │
            └─► ControllerChanged event
                │
                └─► Listeners notified
```

## Handoff Protocol (Used in Phase 4)

```
ModeHandler              IMatrixController        PlaylistHandler   QuickMediaHandler
    │                           │                      │                   │
    │  ◄─── Button Press ───────┐                      │                   │
    │                            │                      │                   │
    │  RequestControlAsync("QM")─┼──────────────────────►                   │
    │                            │                      │                   │
    │  WaitForRelease ◄──────────┼──────── Current("PL") Ready to pause │
    │                            │                      │                   │
    │                            │                      ├─► (finish current) │
    │                            │                      │                    │
    │                            │                      └─► ReleaseControl   │
    │                            │                                │          │
    │                            │◄──────── SemaphoreSlim.Release ┘          │
    │                            │                                           │
    │  ◄─────────────────────────┤ ReceiveControl("QM")                      │
    │                            │                                           │
    │                            │               Now QM can start ────────►  │
    │                            │                                (output)
    │
    │  ... QM plays ...
    │
    │  RequestControlAsync("QM", ReturnTo="PL") ◄─── QM finished
    │
    └────────────────────► Wait for QM release, grant back to PL
```

## Key Design Principles

1. **Exclusive Access** - Only one handler uses matrix at a time
2. **Queue-Based** - Fairness; handlers wait in order
3. **Callback-Only** - No retained references to matrix/canvas (prevents deadlocks)
4. **Synchronous Completion** - All work in the callback must complete (no async)
5. **Clear Ownership** - Handler requested → handler must release (verified)
6. **Resilient Events** - Listener failures don't crash other listeners
7. **Observable** - Debug logs at every state change
