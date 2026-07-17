namespace UMBRAL_Back_end.Tests.TestDoubles;

using MediatR;

/// <summary>
/// <see cref="ISender"/> que lanza la excepción indicada en cualquier envío. Sirve para
/// cubrir, con tests unitarios (sin pipeline HTTP ni Docker), los bloques catch de los
/// controllers: el 500 ante una excepción inesperada y el rethrow de
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class ThrowingSender(Exception ex) : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw ex;

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw ex;

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw ex;

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw ex;

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw ex;
}
