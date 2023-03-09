// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Eventuous.Subscriptions.Context;
using Eventuous.Tools;
using Npgsql;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace Eventuous.Postgresql.Projections;

public abstract class PostgresProjector : EventHandler {
    readonly GetPostgresConnection _getConnection;

    protected PostgresProjector(GetPostgresConnection getConnection, TypeMapper? mapper = null) : base(mapper)
        => _getConnection = getConnection;

    protected void On<T>(ProjectToPostgres<T> handler) where T : class {
        base.On<T>(async ctx => await Handle(ctx, GetCommand).NoContext());

        ValueTask<NpgsqlCommand> GetCommand(NpgsqlConnection connection, MessageConsumeContext<T> context)
            => new(handler(connection, context));
    }

    protected void On<T>(ProjectToPostgresAsync<T> handler) where T : class
        => base.On<T>(async ctx => await Handle(ctx, handler).NoContext());

    async Task Handle<T>(MessageConsumeContext<T> context, ProjectToPostgresAsync<T> handler) where T : class {
        await using var connection = _getConnection();
        await connection.OpenAsync(context.CancellationToken).NoContext();
        var cmd = await handler(connection, context).NoContext();
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).NoContext();
    }

    protected static NpgsqlCommand Project(NpgsqlConnection connection, string commandText, params NpgsqlParameter[] parameters) {
        var cmd = connection.CreateCommand();
        cmd.CommandText = commandText;
        cmd.Parameters.AddRange(parameters);
        cmd.CommandType = CommandType.Text;
        return cmd;
    }
}

public delegate NpgsqlCommand ProjectToPostgres<T>(NpgsqlConnection connection, MessageConsumeContext<T> consumeContext)
    where T : class;

public delegate ValueTask<NpgsqlCommand> ProjectToPostgresAsync<T>(NpgsqlConnection connection, MessageConsumeContext<T> consumeContext)
    where T : class;
