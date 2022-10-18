using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AddonMirror.Extensions;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AddonMirror.Repositories;

/// <inheritdoc />
public class AzureTableStorageRepository : IAzureTableStorageRepository
{
    private const uint CacheLifespanInSeconds = 30;

    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<AddonMirrorOptions> _options;
    private readonly ILogger<AzureTableStorageRepository> _logger;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AzureTableStorageRepository"/> class.
    /// </summary>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    public AzureTableStorageRepository(
        IMemoryCache memoryCache,
        IOptions<AddonMirrorOptions> options,
        ILogger<AzureTableStorageRepository> logger)
    {
        memoryCache.ShouldNotBeNull(nameof(memoryCache));
        options.ShouldNotBeNull(nameof(options));
        logger.ShouldNotBeNull(nameof(logger));

        _memoryCache = memoryCache;
        _options = options;
        _logger = logger;
        _semaphore = new SemaphoreSlim(1);
    }

    /// <inheritdoc />
    public async Task AddTableEntityAsync<T>(
        string tableName,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        tableName.ShouldNotBeNull(nameof(tableName));
        entity.ShouldNotBeNull(nameof(entity));
        entity.PartitionKey.ShouldNotBeNullOrWhiteSpace($"{nameof(entity)}.{nameof(entity.PartitionKey)}");
        entity.RowKey.ShouldNotBeNullOrWhiteSpace($"{nameof(entity)}.{nameof(entity.RowKey)}");

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var tableClient = await GetTableClientAsync(tableName, cancellationToken).ConfigureAwait(false);

            await tableClient
                .AddEntityAsync(
                    entity: entity,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug($"{nameof(AddTableEntityAsync)} took {stopwatch.ElapsedMilliseconds} milliseconds to complete.");
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetTableEntityAsync<T>(
        string tableName,
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        tableName.ShouldNotBeNullOrWhiteSpace(nameof(tableName));
        partitionKey.ShouldNotBeNullOrWhiteSpace(nameof(partitionKey));
        rowKey.ShouldNotBeNullOrWhiteSpace(nameof(rowKey));

        T? entity;

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var tableClient = await GetTableClientAsync(tableName, cancellationToken).ConfigureAwait(false);

            var result = await tableClient
                .GetEntityAsync<T>(
                    partitionKey: partitionKey,
                    rowKey: rowKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            entity = result.Value;
        }
        catch (RequestFailedException rfe) when (rfe.ErrorCode == TableErrorCode.ResourceNotFound)
        {
            entity = null;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug($"{nameof(GetTableEntityAsync)} took {stopwatch.ElapsedMilliseconds} milliseconds to complete.");
        }

        return entity;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetTableEntitiesAsync<T>(
        string tableName,
        string filter,
        CancellationToken cancellationToken)
        where T : class, ITableEntity, new()
    {
        tableName.ShouldNotBeNullOrWhiteSpace(nameof(tableName));
        filter.ShouldNotBeNullOrWhiteSpace(nameof(filter));

        var entities = new List<T>();

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var tableClient = await GetTableClientAsync(tableName, cancellationToken).ConfigureAwait(false);
            var results = tableClient.QueryAsync<T>(filter: filter, cancellationToken: cancellationToken);

            await foreach (var result in results)
            {
                entities.Add(result);
            }
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug($"{nameof(GetTableEntitiesAsync)} took {stopwatch.ElapsedMilliseconds} milliseconds to complete.");
        }

        return entities;
    }

    /// <inheritdoc />
    public async Task UpdateTableEntityAsync<T>(
        string tableName,
        T entity,
        TableUpdateMode mode = TableUpdateMode.Merge,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        tableName.ShouldNotBeNullOrWhiteSpace(nameof(tableName));
        entity.ShouldNotBeNull(nameof(entity));
        entity.PartitionKey.ShouldNotBeNullOrWhiteSpace($"{nameof(entity)}.{nameof(entity.PartitionKey)}");
        entity.RowKey.ShouldNotBeNullOrWhiteSpace($"{nameof(entity)}.{nameof(entity.RowKey)}");

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var tableClient = await GetTableClientAsync(tableName, cancellationToken).ConfigureAwait(false);

            var result = await tableClient
                .UpdateEntityAsync(
                    entity: entity,
                    ifMatch: entity.ETag,
                    mode: mode,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug($"{nameof(UpdateTableEntityAsync)} took {stopwatch.ElapsedMilliseconds} milliseconds to complete.");
        }
    }

    /// <inheritdoc />
    public async Task UpsertTableEntityAsync<T>(
        string tableName,
        T entity,
        TableUpdateMode mode = TableUpdateMode.Merge,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        tableName.ShouldNotBeNullOrWhiteSpace(nameof(tableName));
        entity.ShouldNotBeNull(nameof(entity));
        entity.PartitionKey.ShouldNotBeNullOrWhiteSpace($"{nameof(entity)}.{nameof(entity.PartitionKey)}");
        entity.RowKey.ShouldNotBeNullOrWhiteSpace($"{nameof(entity)}.{nameof(entity.RowKey)}");

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var tableClient = await GetTableClientAsync(tableName, cancellationToken).ConfigureAwait(false);

            var result = await tableClient
                .UpsertEntityAsync(
                    entity: entity,
                    mode: mode,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug($"{nameof(UpsertTableEntityAsync)} took {stopwatch.ElapsedMilliseconds} milliseconds to complete.");
        }
    }

    /// <inheritdoc />
    public async Task DeleteTableEntityAsync(
        string tableName,
        string partitionKey,
        string rowKey,
        ETag ifMatch = default,
        CancellationToken cancellationToken = default)
    {
        tableName.ShouldNotBeNullOrWhiteSpace(nameof(tableName));
        partitionKey.ShouldNotBeNullOrWhiteSpace(nameof(partitionKey));
        rowKey.ShouldNotBeNullOrWhiteSpace(nameof(rowKey));

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var tableClient = await GetTableClientAsync(tableName, cancellationToken).ConfigureAwait(false);

            var result = await tableClient
                .DeleteEntityAsync(
                    partitionKey: partitionKey,
                    rowKey: rowKey,
                    ifMatch: ifMatch,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug($"{nameof(DeleteTableEntityAsync)} took {stopwatch.ElapsedMilliseconds} milliseconds to complete.");
        }
    }

    /// <inheritdoc />
    public async Task SubmitTransactionAsync(
        string tableName,
        IEnumerable<TableTransactionAction> transactionActions,
        CancellationToken cancellationToken = default)
    {
        tableName.ShouldNotBeNullOrWhiteSpace(nameof(tableName));
        transactionActions.ShouldNotBeNull(nameof(transactionActions));

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var tableClient = await GetTableClientAsync(tableName, cancellationToken).ConfigureAwait(false);

            var result = await tableClient
                .SubmitTransactionAsync(
                    transactionActions: transactionActions,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug($"{nameof(DeleteTableEntityAsync)} took {stopwatch.ElapsedMilliseconds} milliseconds to complete.");
        }
    }

    private async Task<TableClient> GetTableClientAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        tableName.ShouldNotBeNullOrWhiteSpace(nameof(tableName));
        _options.Value.ShouldNotBeNull($"{nameof(_options)}.{nameof(_options.Value)}");
        _options.Value.AzureStorageConnectionString.ShouldNotBeNull($"{nameof(_options)}.{nameof(_options.Value)}.{_options.Value.AzureStorageConnectionString}");

        var cacheKey = $"{nameof(AzureTableStorageRepository)}:{nameof(GetTableClientAsync)}:{tableName}";

        if (!_memoryCache.TryGetValue(cacheKey, out TableClient tableClient))
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (!_memoryCache.TryGetValue(cacheKey, out tableClient))
                {
                    tableClient = new TableClient(_options.Value.AzureStorageConnectionString, tableName);

                    await tableClient.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

                    _memoryCache.Set(cacheKey, tableClient, TimeSpan.FromSeconds(CacheLifespanInSeconds));
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        return tableClient;
    }
}
