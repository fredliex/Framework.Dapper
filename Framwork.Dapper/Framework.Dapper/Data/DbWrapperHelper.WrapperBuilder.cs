using System;
using System.Collections.Generic;
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
        private static class WrapperBuilder<TProviderFactory> where TProviderFactory : DbProviderFactory
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
                    var types = new[] { connType, transType, command.GetType(), parameter.GetType(), command.Parameters.GetType() };
                    command.Dispose();
                    var interceptType = typeof(Intercept<,,,,>).MakeGenericType(types);
                    wrapper = (Func<DbProviderFactory, DbProviderFactory>)interceptType.GetField(nameof(Intercept<DbConnection, DbTransaction, DbCommand, DbParameter, DbParameterCollection>.wrapProviderFactory)).GetValue(null);
                    Interlocked.Exchange(ref providerFactoryWrapper, wrapper);
                }
                return wrapper(factory);
            }

            private static class Intercept<TConnection, TTransaction, TCommand, TParameter, TParameterCollection>
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

                static Intercept()
                {
                    SetWrapper(typeof(TProviderFactory), out wrapProviderFactory);
                    SetWrapper(typeof(TConnection), out wrapConnection);
                    SetWrapper(typeof(TTransaction), out wrapTransaction);
                    SetWrapper(typeof(TCommand), out wrapCommand);
                    SetWrapper(typeof(TParameter), out wrapParameter);
                    SetWrapper(typeof(TParameterCollection), out wrapParameterCollection);
                }

                #region ProviderFactoryWrapper
                public abstract class WrappedProviderFactory : DbProviderFactory, IWrappedDb<TProviderFactory>
                {
                    public abstract TProviderFactory Instance { get; }
                }
                #endregion

                #region WrappedConnection
                public abstract class WrappedConnection : DbConnection, IWrappedDb<TConnection>
                {
                    public abstract TConnection Instance { get; }
                }
                #endregion

                #region WrappedTransaction
                public abstract class WrappedTransaction : DbTransaction, IWrappedDb<TTransaction>
                {
                    public abstract TTransaction Instance { get; }
                }
                #endregion

                #region WrappedCommand
                public abstract class WrappedCommand : DbCommand, IWrappedDb<TCommand>
                {
                    public abstract TCommand Instance { get; }
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
                }
                #endregion
            }
        }
    }
}
