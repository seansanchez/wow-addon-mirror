using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace AddonMirror.Repositories;

/// <summary>
///     Provides common operations for table storage repositories.
/// </summary>
public interface IAzureTableStorageRepository
{
    /// <summary>
    ///     Creates a new table entity but does not merge with or overwrite an existing table entity.
    /// </summary>
    /// <typeparam name="T">The type of table entity.</typeparam>
    /// <param name="tableName">The table name.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns a task representing the completion of the operation.</returns>
    Task AddTableEntityAsync<T>(
        string tableName,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();

    /// <summary>
    ///     Gets the specified table entity.
    /// </summary>
    /// <typeparam name="T">The type of table entity.</typeparam>
    /// <param name="tableName">The table name.</param>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="rowKey">The row key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns a task containing the specified table entity.</returns>
    Task<T?> GetTableEntityAsync<T>(
        string tableName,
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();

    /// <summary>
    ///     Gets the specified table entities.
    /// </summary>
    /// <typeparam name="T">The type of table entity.</typeparam>
    /// <param name="tableName">The table name.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns a task containing the specified table entities.</returns>
    Task<IEnumerable<T>> GetTableEntitiesAsync<T>(
        string tableName,
        string filter,
        CancellationToken cancellationToken)
        where T : class, ITableEntity, new();

    /// <summary>
    ///     Updates an existing table entity.
    /// </summary>
    /// <typeparam name="T">The type of table entity.</typeparam>
    /// <param name="tableName">The table name.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="mode">The table update mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns a task representing the completion of the operation.</returns>
    Task UpdateTableEntityAsync<T>(
        string tableName,
        T entity,
        TableUpdateMode mode = TableUpdateMode.Merge,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();

    /// <summary>
    ///     Replaces the table entity, if it exists. Creates a new table entity if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type of table entity.</typeparam>
    /// <param name="tableName">The table name.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="mode">The table update mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns a task representing the completion of the operation.</returns>
    Task UpsertTableEntityAsync<T>(
        string tableName,
        T entity,
        TableUpdateMode mode = TableUpdateMode.Merge,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();

    /// <summary>
    ///     Deletes an existing table entity.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="rowKey">The row key.</param>
    /// <param name="ifMatch">
    /// The If-Match value to be used for optimistic concurrency.
    /// If <see cref="F:Azure.ETag.All" /> is specified, the operation will be executed unconditionally.
    /// If the <see cref="P:Azure.Data.Tables.ITableEntity.ETag" /> value is specified, the operation will fail with a status of 412 (Precondition Failed)
    /// if the <see cref="T:Azure.ETag" /> value of the entity in the table does not match.
    /// The default is to delete unconditionally.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns a task representing the completion of the operation.</returns>
    Task DeleteTableEntityAsync(
        string tableName,
        string partitionKey,
        string rowKey,
        ETag ifMatch = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Submits the batch transaction.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="transactionActions">The transaction actions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns a task representing the completion of the operation.</returns>
    Task SubmitTransactionAsync(
        string tableName,
        IEnumerable<TableTransactionAction> transactionActions,
        CancellationToken cancellationToken = default);
}
