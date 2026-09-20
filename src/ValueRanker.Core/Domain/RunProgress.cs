namespace ValueRanker.Core.Domain;

/// <summary>
/// An honest estimate of where a run stands: which of the three phases it's in, and roughly how
/// many steps into that phase. EstimatedStepsInPhase is an approximation (the Finale's length is
/// adaptive, so it's capped at RankingOptions.MaxDuels rather than a fixed count).
/// </summary>
public sealed record RunProgress(
    RankingPhase Phase,
    int PhaseNumber,
    int TotalPhases,
    int StepsCompletedInPhase,
    int EstimatedStepsInPhase);
