namespace ValueRanker.Core.Domain;

public interface IRankingStrategy
{
    RankingState Apply(RankingState state, RankingEvent evt);

    NextStep GetNextStep(RankingState state);

    bool IsFinished(RankingState state);
}
