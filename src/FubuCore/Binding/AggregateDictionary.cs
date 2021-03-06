using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web;
using System.Web.Routing;
using FubuCore.Reflection;
using FubuCore.Util;

namespace FubuCore.Binding
{
    public enum RequestDataSource
    {
        Route,
        Request,
        RequestProperty,
        File,
        Header,
        Other
    }


    public class AggregateDictionary
    {
        private readonly IList<Locator> _locators = new List<Locator>();
        private static readonly Cache<string, Func<HttpRequestBase, object>> _requestProperties = new Cache<string, Func<HttpRequestBase, object>>();
        private static readonly IList<PropertyInfo> _systemProperties = new List<PropertyInfo>();


        static AggregateDictionary()
        {
            AddRequestProperty(r => r.AcceptTypes);
            AddRequestProperty(r => r.ApplicationPath);
            AddRequestProperty(r => r.AppRelativeCurrentExecutionFilePath);
            AddRequestProperty(r => r.Browser);
            AddRequestProperty(r => r.ClientCertificate);
            AddRequestProperty(r => r.ContentEncoding);
            AddRequestProperty(r => r.ContentLength);
            AddRequestProperty(r => r.ContentType);
            AddRequestProperty(r => r.Cookies);
            AddRequestProperty(r => r.CurrentExecutionFilePath);
            AddRequestProperty(r => r.FilePath);
            AddRequestProperty(r => r.Files);
            AddRequestProperty(r => r.Filter);
            AddRequestProperty(r => r.Form);
            AddRequestProperty(r => r.Headers);
            AddRequestProperty(r => r.HttpMethod);
            AddRequestProperty(r => r.IsAuthenticated);
            AddRequestProperty(r => r.IsLocal);
            AddRequestProperty(r => r.IsSecureConnection);
            AddRequestProperty(r => r.LogonUserIdentity);
            AddRequestProperty(r => r.Params);
            AddRequestProperty(r => r.Path);
            AddRequestProperty(r => r.PathInfo);
            AddRequestProperty(r => r.PhysicalApplicationPath);
            AddRequestProperty(r => r.PhysicalPath);
            AddRequestProperty(r => r.QueryString);
            AddRequestProperty(r => r.RawUrl);
            AddRequestProperty(r => r.RequestType);
            AddRequestProperty(r => r.ServerVariables);
            AddRequestProperty(r => r.TotalBytes);
            AddRequestProperty(r => r.Url);
            AddRequestProperty(r => r.UrlReferrer);
            AddRequestProperty(r => r.UserAgent);
            AddRequestProperty(r => r.UserHostAddress);
            AddRequestProperty(r => r.UserHostName);
            AddRequestProperty(r => r.UserLanguages);
        }

        public static bool IsSystemProperty(PropertyInfo property)
        {
            return
                _systemProperties.Any(
                    x => property.PropertyType.IsAssignableFrom(x.PropertyType) && x.Name == property.Name);
        }

        public AggregateDictionary()
        {
        }

        public AggregateDictionary(RequestContext context)
        {
            AddLocator(RequestDataSource.Route, key =>
            {
                object found;
                context.RouteData.Values.TryGetValue(key, out found);
                return found;
            });

            HttpContextBase @base = context.HttpContext;

            configureForRequest(@base);
        }

        public static void AddRequestProperty(Expression<Func<HttpRequestBase, object>> expression)
        {
            var property = ReflectionHelper.GetProperty(expression);
            _systemProperties.Add(property);

            _requestProperties[property.Name] = expression.Compile();
        }

        public static AggregateDictionary ForHttpContext(HttpContextWrapper context)
        {
            var dict = new AggregateDictionary();
            dict.configureForRequest(context);

            return dict;
        }

        private void configureForRequest(HttpContextBase @base)
        {
            HttpRequestBase request = @base.Request;

            AddLocator(RequestDataSource.Request, key => request[key]);
            AddLocator(RequestDataSource.File, key => request.Files[key]);
            AddLocator(RequestDataSource.Header, key => request.Headers[key]);
            AddLocator(RequestDataSource.RequestProperty, key => GetRequestProperty(request, key));
        }

        private static object GetRequestProperty(HttpRequestBase request, string key)
        {
            return _requestProperties.Has(key) ? _requestProperties[key](request) : null;
        }

        public void Value(string key, Action<RequestDataSource, object> callback)
        {
            _locators.Any(x => x.Locate(key, callback));
        }

        public AggregateDictionary AddLocator(RequestDataSource source, Func<string, object> locator)
        {
            _locators.Add(new Locator
            {
                Getter = locator,
                Source = source
            });

            return this;
        }

        public AggregateDictionary AddDictionary(IDictionary<string, object> dictionary)
        {
            AddLocator(RequestDataSource.Other, key => dictionary.ContainsKey(key) ? dictionary[key] : null);
            return this;
        }

        #region Nested type: Locator

        public class Locator
        {
            public RequestDataSource Source { get; set; }
            public Func<string, object> Getter { get; set; }

            public bool Locate(string key, Action<RequestDataSource, object> callback)
            {
                object value = Getter(key);
                if (value != null)
                {
                    callback(Source, value);
                    return true;
                }

                return false;
            }
        }

        #endregion
    }
}