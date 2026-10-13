namespace GW_Launcher.Launcher;

// One step of a long-running task, for any progress view. A null Fraction means working, with no measure of how far.
public sealed record ProgressUpdate(double? Fraction, string Detail);
