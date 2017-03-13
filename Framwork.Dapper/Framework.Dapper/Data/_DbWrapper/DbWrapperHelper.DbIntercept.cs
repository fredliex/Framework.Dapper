using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class DbWrapperHelper
    {
        private static class DbIntercept<TProviderFactory> where TProviderFactory : DbProviderFactory
        {
            private static Func<DbProviderFactory, DbProviderFactory> providerFactoryWrapper = null;
            public static DbProviderFactory WrapDbProviderFactory(DbProviderFactory factory)
            {
                var wrapper = providerFactoryWrapper;
                if (wrapper == null)
                {
                    var conn = factory.CreateConnection();
                    var connType = conn.GetType();
                    conn.Dispose();
                    var transType = connType.GetMethod(nameof(DbConnection.BeginTransaction), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null).ReturnType;
                    var command = factory.CreateCommand();
                    var parameter = factory.CreateParameter();
                    var types = new[] { factory.GetType(), connType, transType, command.GetType(), parameter.GetType(), command.Parameters.GetType() };
                    command.Dispose();
                    var interceptType = typeof(DbIntercept<,,,,,>).MakeGenericType(types);
                    //呼叫DbIntercept.InitProviderAndGetWrapper
                    const string methodName = nameof(DbIntercept<DbProviderFactory, DbConnection, DbTransaction, DbCommand, DbParameter, DbParameterCollection>.InitProviderAndGetWrapper);
                    wrapper = (Func<DbProviderFactory, DbProviderFactory>)interceptType.GetMethod(methodName).Invoke(null, new[] { factory });
                    Interlocked.Exchange(ref providerFactoryWrapper, wrapper);
                }
                return wrapper(factory);
            }
        }

        public static class DbIntercept<TProviderFactory, TConnection, TTransaction, TCommand, TParameter, TParameterCollection>
            where TProviderFactory : DbProviderFactory
            where TConnection : DbConnection
            where TTransaction : DbTransaction
            where TCommand : DbCommand
            where TParameter : DbParameter
            where TParameterCollection : DbParameterCollection
        {
            public static readonly Func<DbProviderFactory, WrappedProviderFactory> wrapProviderFactory;
            public static readonly Func<DbConnection, WrappedConnection> wrapConnection;
            public static readonly Func<DbTransaction, WrappedTransaction> wrapTransaction;
            public static readonly Func<DbCommand, WrappedCommand> wrapCommand;
            public static readonly Func<DbParameter, WrappedParameter> wrapParameter;
            public static readonly Func<DbParameterCollection, WrappedParameterCollection> wrapParameterCollection;

            private static WrappedProviderFactory providerFactory = null;

            static DbIntercept()
            {
                SetWrapper(typeof(TProviderFactory), out wrapProviderFactory);
                SetWrapper(typeof(TConnection), out wrapConnection);
                SetWrapper(typeof(TTransaction), out wrapTransaction);
                SetWrapper(typeof(TCommand), out wrapCommand);
                SetWrapper(typeof(TParameter), out wrapParameter);
                SetWrapper(typeof(TParameterCollection), out wrapParameterCollection);
            }

            public static Func<DbProviderFactory, WrappedProviderFactory> InitProviderAndGetWrapper(DbProviderFactory factory)
            { 
                providerFactory = wrapProviderFactory(factory);
                return wrapProviderFactory;
            }

            #region ProviderFactoryWrapper
            public abstract class WrappedProviderFactory : DbProviderFactory, IWrappedDb<TProviderFactory>
            {
                public abstract TProviderFactory Instance { get; }
                public override DbConnection CreateConnection() => wrapConnection(Instance.CreateConnection());
                public override DbCommand CreateCommand() => wrapCommand(Instance.CreateCommand());
                public override DbParameter CreateParameter() => wrapParameter(Instance.CreateParameter());
            }
            #endregion

            #region WrappedConnection
            public abstract class WrappedConnection : DbConnectionWrapper, IWrappedDb<TConnection>
            {
                public abstract TConnection Instance { get; }
                protected override DbProviderFactory DbProviderFactory => providerFactory;
                protected override DbCommand CreateDbCommand()
                {
                    var command = wrapCommand(Instance.CreateCommand());
                    command.WrappedConn = this;
                    return command;
                }
                protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
                {
                    var trans = wrapTransaction(Instance.BeginTransaction(isolationLevel));
                    trans.WrappedConn = this;
                    return trans;
                }
            }
            #endregion

            #region WrappedTransaction
            public abstract class WrappedTransaction : DbTransaction, IWrappedDb<TTransaction>
            {
                internal WrappedConnection WrappedConn = null;
                public abstract TTransaction Instance { get; }
                protected override DbConnection DbConnection => WrappedConn;
            }
            #endregion

            #region WrappedCommand
            public abstract class WrappedCommand : DbCommand, IWrappedDb<TCommand>
            {
                internal WrappedConnection WrappedConn= null;
                internal WrappedTransaction WrappedTrans = null;
                private WrappedParameterCollection wrappedParams = null;
                public abstract TCommand Instance { get; }
                protected override DbConnection DbConnection {
                    get => WrappedConn;
                    set => Instance.Connection = ((WrappedConn = (WrappedConnection)value)).Instance;
                }
                protected override DbTransaction DbTransaction {
                    get => WrappedTrans;
                    set => Instance.Transaction = ((WrappedTrans = (WrappedTransaction)value)).Instance;
                }
                protected override DbParameterCollection DbParameterCollection => wrappedParams ?? (wrappedParams = wrapParameterCollection(Instance.Parameters));
                protected override DbParameter CreateDbParameter() => wrapParameter(Instance.CreateParameter());

                protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => 
                    dbCommandIntercept?.CommandExecute(Instance, cmd => cmd.ExecuteReader(behavior)) ?? Instance.ExecuteReader(behavior);

                public override int ExecuteNonQuery() =>
                    dbCommandIntercept?.CommandExecute(Instance, cmd => cmd.ExecuteNonQuery()) ?? Instance.ExecuteNonQuery();

                public override object ExecuteScalar() =>
                    dbCommandIntercept?.CommandExecute(Instance, cmd => cmd.ExecuteScalar()) ?? Instance.ExecuteScalar();
            }
            #endregion

            #region WrappedParameter
            public abstract class WrappedParameter : DbParameter, IWrappedDb<TParameter>
            {
                public abstract TParameter Instance { get; }
            }
            #endregion

            #region WrappedParameterCollection
            public abstract class WrappedParameterCollection : DbParameterCollection, IWrappedDb<TParameterCollection>
            {
                public abstract TParameterCollection Instance { get; }
                private Dictionary<TParameter, WrappedParameter> mappings = new Dictionary<TParameter, WrappedParameter>();

                protected override DbParameter GetParameter(int index) => mappings[(TParameter)Instance[index]];
                protected override DbParameter GetParameter(string parameterName) => mappings[(TParameter)Instance[parameterName]];
                protected override void SetParameter(int index, DbParameter value)
                {
                    var wrap = (WrappedParameter)value;
                    var old = (TParameter)Instance[index];
                    Instance[index] = wrap.Instance;
                    mappings.Remove(old);
                    mappings.Add(wrap.Instance, wrap);
                }
                protected override void SetParameter(string parameterName, DbParameter value)
                {
                    var wrap = (WrappedParameter)value;
                    var old = (TParameter)Instance[parameterName];
                    Instance[parameterName] = wrap.Instance;
                    mappings.Remove(old);
                    mappings.Add(wrap.Instance, wrap);
                }
                public override void CopyTo(Array array, int index) => ((ICollection)mappings.Values).CopyTo(array, index);
                public override int Add(object value)
                {
                    var wrap = (WrappedParameter)value;
                    mappings.Add(wrap.Instance, wrap);
                    return Instance.Add(wrap.Instance);
                }
                public override void AddRange(Array values)
                {
                    foreach (object obj in values) Add(obj);
                }
                public override void Insert(int index, object value)
                {
                    var wrap = (WrappedParameter)value;
                    mappings.Add(wrap.Instance, wrap);
                    Instance.Insert(index, wrap.Instance);
                }
                public override void Remove(object value)
                {
                    var wrap = (WrappedParameter)value;
                    mappings.Remove(wrap.Instance);
                    Instance.Remove(wrap.Instance);
                }
                public override void RemoveAt(int index)
                {
                    var n = Instance[index];
                    Instance.RemoveAt(index);
                    mappings.Remove((TParameter)n);
                }
                public override void RemoveAt(string parameterName)
                {
                    var n = Instance[parameterName];
                    Instance.RemoveAt(parameterName);
                    mappings.Remove((TParameter)n);
                }
                public override void Clear()
                {
                    Instance.Clear();
                    mappings.Clear();
                }

                public override int IndexOf(object value) => Instance.IndexOf(((WrappedParameter)value).Instance);
                public override bool Contains(object value) => Instance.Contains(((WrappedParameter)value).Instance);

                //要按照原本的順序
                public override IEnumerator GetEnumerator() => Instance.OfType<TParameter>().Select(x => mappings[x]).ToList().GetEnumerator();
            }
            #endregion
        }
    }
}
