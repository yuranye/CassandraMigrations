using Cassandra;

public class MigrationsRetryPolicy : IRetryPolicy
{
    private readonly int _readAttempts;
    private readonly int _writeAttempts;
    private readonly int _unavailableAttempts;

    public MigrationsRetryPolicy(int readAttempts, int writeAttempts, int unavailableAttempts)
    {
        _readAttempts = readAttempts;
        _writeAttempts = writeAttempts;
        _unavailableAttempts = unavailableAttempts;
    }

    public RetryDecision OnReadTimeout(IStatement query, ConsistencyLevel cl, int requiredResponses,
        int receivedResponses, bool dataRetrieved, int nbRetry)
    {
        if (dataRetrieved)
        {
            return RetryDecision.Ignore();
        }

        return nbRetry < _readAttempts
            ? RetryDecision.Retry(cl)
            : RetryDecision.Rethrow();
    }

    public RetryDecision OnUnavailable(IStatement query, ConsistencyLevel cl, int requiredReplica, 
        int aliveReplica, int nbRetry) =>
        nbRetry < _unavailableAttempts
            ? RetryDecision.Retry(ConsistencyLevel.One)
            : RetryDecision.Rethrow();

    public RetryDecision OnWriteTimeout(IStatement query, ConsistencyLevel cl, string writeType, int requiredAcks,
        int receivedAcks, int nbRetry) =>
        nbRetry < _writeAttempts
            ? RetryDecision.Retry(cl)
            : RetryDecision.Rethrow();
}