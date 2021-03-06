﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    public static class DbModelHelper
    {
        #region model/object to Dictionary

        private static ConcurrentDictionary<Type, Action<IDictionary<string, object>, object>> cacheDictionaryFillers =
            new ConcurrentDictionary<Type, Action<IDictionary<string, object>, object>>();

        private static Dictionary<string, object> ModelToDictionary(object model)
        {
            var filler = cacheDictionaryFillers.GetOrAdd(model.GetType(), ModelColumnInfo.GenerateDictionaryFiller);
            var dict = new Dictionary<string, object>();
            filler(dict, model);
            return dict;
        }

        public static IDictionary<string, object> ToDictionary(object model) => 
            model == null ? null :
            model is IDictionary<string, object> dict ? InternalDbHelper.WrapDictionaryParam(dict) :
            ModelToDictionary(model);

        public static Dictionary<string, object> ToDictionary(this IDbModel model) =>
            model == null ? null : DbModelHelper.ModelToDictionary(model);

        #endregion

        public static ModelMerger<T> Merge<T>(this IEnumerable<T> oldModels, IEnumerable<T> newModels, params Expression<Func<T, object>>[] keyExpressions) where T : IDbModel =>
            new ModelMerger<T>(oldModels, newModels, keyExpressions);
    }
}
