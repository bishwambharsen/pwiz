﻿using System;
using System.Collections.Generic;
using System.Threading;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public abstract class RemoteSession : IDisposable
    {
        private readonly object _lock = new object();
        private readonly IDictionary<RequestKey, RemoteResponse> _responses
            = new Dictionary<RequestKey, RemoteResponse>();
        private readonly HashSet<RequestKey> _fetchRequests
            = new HashSet<RequestKey>();
        protected readonly CancellationTokenSource _cancellationTokenSource 
            = new CancellationTokenSource();

        protected RemoteSession(RemoteAccount account)
        {
            Account = account;
        }
        protected void FireContentsAvailable()
        {
            var contentsAvailable = ContentsAvailable;
            if (contentsAvailable != null)
            {
                contentsAvailable();
            }
        }

        public event Action ContentsAvailable;
        
        public virtual void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        public RemoteAccount Account { get; private set; }

        public abstract IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl);

        public abstract bool AsyncFetchContents(RemoteUrl chorusUrl, out RemoteServerException remoteException);

        protected bool AsyncFetch<T>(Uri requestUri, Func<Uri, T> fetcher, out RemoteServerException remoteException) where T : IResponseData
        {
            if (null == requestUri)
            {
                remoteException = null;
                return true;
            }
            lock (_lock)
            {
                RemoteResponse response;
                var key = new RequestKey(typeof(T), requestUri);
                if (_responses.TryGetValue(key, out response))
                {
                    remoteException = response.Exception;
                    return true;
                }
                remoteException = null;
                if (!_fetchRequests.Add(key))
                {
                    return false;
                }
            }
            ActionUtil.RunAsync(()=>FetchAndStore(requestUri, fetcher));
            return false;
        }

        protected void RetryFetch<T>(Uri requestUri, Func<Uri, T> fetcher) where T : IResponseData
        {
            var key = new RequestKey(typeof(T), requestUri);
            lock (_lock)
            {
                
                if (_fetchRequests.Contains(key))
                {
                    return;
                }
                _responses.Remove(key);
                RemoteServerException exceptionIgnore;
                AsyncFetch(requestUri, fetcher, out exceptionIgnore);
            }

        }

        private void FetchAndStore<T>(Uri requestUri, Func<Uri, T> fetcher) where T : IResponseData
        {
            var key = new RequestKey(typeof(T), requestUri);
            try
            {
                var data = fetcher(requestUri);
                lock (_lock)
                {
                    _responses[key] = new RemoteResponse(data);
                }
            }
            catch (Exception exception)
            {
                RemoteServerException remoteException = exception as RemoteServerException;
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (null == remoteException)
                {
                    remoteException = new RemoteServerException(
                        Resources.ChorusSession_FetchContents_There_was_an_error_communicating_with_the_server__
                        + exception.Message, exception);
                }
                lock (_lock)
                {
                    _responses[key] = new RemoteResponse(remoteException);
                }
            }
            FireContentsAvailable();
        }

        protected bool TryGetData<T>(Uri requestUri, out T data) where T : IResponseData
        {
            lock (_lock)
            {
                RemoteResponse remoteResponse;
                if (!_responses.TryGetValue(new RequestKey(typeof(T), requestUri), out remoteResponse))
                {
                    data = default(T);
                    return false;
                }
                data = (T) remoteResponse.Data;
                return true;
            }
        }

        public static RemoteSession CreateSession(RemoteAccount remoteAccount)
        {
            var chorusAccount = remoteAccount as ChorusAccount;
            if (chorusAccount != null)
            {
                return new ChorusSession(chorusAccount);
            }
            throw new ArgumentException();
        }

        public abstract void RetryFetchContents(RemoteUrl chorusUrl);

        protected class RemoteResponse
        {
            public RemoteResponse(IResponseData data)
            {
                Data = data;
            }

            public RemoteResponse(RemoteServerException exception)
            {
                Exception = exception;
            }

            public IResponseData Data { get; private set; }
            public RemoteServerException Exception { get; private set; }
        }

        public interface IResponseData
        {
        }

        private struct RequestKey
        {
            public RequestKey(Type type, Uri uri) : this()
            {
                Type = type;
                Uri = uri;
            }

            public Type Type { get; private set; }
            public Uri Uri { get; private set; }
        }
    }
}
