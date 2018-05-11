﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    [XmlRoot("unify_account")]
    public class UnifiAccount : RemoteAccount
    {
        public static readonly UnifiAccount DEFAULT 
            = new UnifiAccount(string.Empty, string.Empty)
        {
            ServerUrl = "https://unifiapi.waters.com:50034",
            IdentityServer = "https://unifiapi.waters.com:50333",
            ClientScope = "unifi",
            ClientSecret = "secret",
        };
        public UnifiAccount(string username, string password)
        {
            Username = username;
            Password = password;

        }

        public string IdentityServer { get; private set; }
        public string ClientScope { get; private set; }
        public string ClientSecret { get; private set; }

        private enum ATTR
        {
            identity_server,
            client_scope,
            client_secret,
        }

        protected override void ReadXElement(XElement xElement)
        {
            base.ReadXElement(xElement);
            IdentityServer = (string) xElement.Attribute(ATTR.identity_server.ToString());
            ClientScope = (string) xElement.Attribute(ATTR.client_scope.ToString());
            ClientSecret = (string) xElement.Attribute(ATTR.client_secret.ToString());
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeString(ATTR.identity_server, IdentityServer);
            writer.WriteAttributeString(ATTR.client_scope, ClientScope);
            writer.WriteAttributeString(ATTR.client_secret, ClientSecret);
        }

        public string GetFoldersUrl()
        {
            return ServerUrl + "/unifi/v1/folders";
        }

        public TokenResponse Authenticate()
        {
            var tokenClient = new TokenClient(IdentityServer + "/identity/connect/token", "resourceownerclient",
                ClientSecret, new HttpClientHandler());
            return tokenClient.RequestResourceOwnerPasswordAsync(Username, Password, ClientScope).Result;
        }

        public IEnumerable<UnifiFolderObject> GetFolders()
        {
            var httpClient = GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(GetFoldersUrl()).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);

            var foldersValue = jsonObject["value"] as JArray;
            if (foldersValue == null)
            {
                return new UnifiFolderObject[0];
            }
            return foldersValue.OfType<JObject>().Select(f => new UnifiFolderObject(f));
        }

        public IEnumerable<UnifiFileObject> GetFiles(UnifiFolderObject folder)
        {
            var httpClient = GetAuthenticatedHttpClient();
            string url = string.Format("/unifi/v1/folders({0})/items", folder.Id);
            var response = httpClient.GetAsync(ServerUrl + url).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);
            var itemsValue = jsonObject["value"] as JArray;
            if (itemsValue == null)
            {
                return new UnifiFileObject[0];
            }
            return itemsValue.OfType<JObject>().Select(f => new UnifiFileObject(f));
        }

        public HttpClient GetAuthenticatedHttpClient()
        {
            var tokenResponse = Authenticate();
            var httpClient = new HttpClient();
            httpClient.SetBearerToken(tokenResponse.AccessToken);
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json;odata.metadata=minimal");
            return httpClient;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.UNIFI; }
        }

        public override RemoteSession CreateSession()
        {
            return new UnifiSession(this);
        }

        public override RemoteUrl GetRootUrl()
        {
            return UnifiUrl.EMPTY.ChangeServerUrl(ServerUrl).ChangeUsername(Username);
        }

        private UnifiAccount()
        {
        }
        public static UnifiAccount Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new UnifiAccount());
        }

        protected bool Equals(UnifiAccount other)
        {
            return base.Equals(other) && string.Equals(IdentityServer, other.IdentityServer) &&
                   string.Equals(ClientScope, other.ClientScope) && string.Equals(ClientSecret, other.ClientSecret);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((UnifiAccount) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (IdentityServer != null ? IdentityServer.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ClientScope != null ? ClientScope.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ClientSecret != null ? ClientSecret.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
