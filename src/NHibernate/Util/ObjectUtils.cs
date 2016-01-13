using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Proxy;

namespace NHibernate.Util
{
    public sealed class ObjectUtils
    {
        private ObjectUtils()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public static object DefaultIfNull(object obj, object defaultVal)
        {
            if (obj == null)
            {
                return defaultVal;
            }
            else
            {
                return obj;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <returns></returns>
        public new static bool Equals(object obj1, object obj2)
        {
            return object.Equals(obj1, obj2);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string IdentityToString(object obj)
        {
            if (obj == null)
            {
                return "null";
            }

            var proxy = obj as INHibernateProxy;

            if (null != proxy)
            {
                var init = proxy.HibernateLazyInitializer;
                return string.Format("{0}#{1}", StringHelper.Unqualify(init.EntityName), init.Identifier);
            }
            return string.Format("{0}@{1}(hash)", StringHelper.Unqualify(obj.GetType().FullName), obj.GetHashCode());
        }
    }
}
