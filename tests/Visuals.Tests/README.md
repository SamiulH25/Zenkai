# Aura clipping regression

Run from the repository root:

	dotnet run --project tests/Visuals.Tests -c Release

The test links the production AuraRenderer and AuraStyle unchanged. Small graphics
stand-ins replace texture uploads so no game, display, GPU or NuGet test packages
are needed. Requires the installed .NET 10 SDK.

Checks 3,600 frames across all 12 palette definitions, three seeds, charging,
hovering, full-speed and overspeed flight, plus forced simultaneous peak-length
tongues. Every frame must have visible aura pixels and two art pixels of clear
padding on all four canvas edges. It also checks same-tick texture reuse and disposal.
The pre-fix 64-art-pixel canvas fails on the first Ki charging case.

This is a CPU rasterization regression, not a visual-quality or flight-animation
test. Run tools/shots.sh for actual tModLoader rendering; ki_flight_hover* and
ki_flight_level* capture successive animation phases, ki_flight_up/down/land
cover direction changes and stopping, and form_ssj3_flight stresses a long aura.
