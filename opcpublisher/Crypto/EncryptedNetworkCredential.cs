using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;


namespace OpcPublisher.Crypto
{
    /// <summary>
    /// Provides in-memory access to encrypted credentials. It uses the OPC UA app cert to encrypt/decrypt credentials.
    /// </summary>
    public class EncryptedNetworkCredential : NetworkCredential
    {
        public async static Task<EncryptedNetworkCredential> FromPlainCredential(string username, string password) => await Encrypt(new NetworkCredential(username, password));

        public async static Task<EncryptedNetworkCredential> Encrypt(NetworkCredential networkCredential)
        {
            EncryptedNetworkCredential result = new EncryptedNetworkCredential();

            X509Certificate2 cert = await OpcApplicationConfiguration.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null).ConfigureAwait(false);

            if (networkCredential.UserName != null)
            {
                result.UserName = Convert.ToBase64String(cert.GetRSAPublicKey().EncryptValue(Convert.FromBase64String(networkCredential.UserName)));
            }
            
            if (networkCredential.Password != null)
            {
                result.Password = Convert.ToBase64String(cert.GetRSAPublicKey().EncryptValue(Convert.FromBase64String(networkCredential.Password)));
            }

            return result;
        }

        public async Task<NetworkCredential> Decrypt()
        {
            NetworkCredential result = new NetworkCredential();

            X509Certificate2 cert = await OpcApplicationConfiguration.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null).ConfigureAwait(false);

            if (UserName != null)
            {
                result.UserName = Convert.ToBase64String(cert.GetRSAPrivateKey().DecryptValue(Convert.FromBase64String(UserName)));
            }

            if (Password != null)
            {
                result.Password = Convert.ToBase64String(cert.GetRSAPrivateKey().DecryptValue(Convert.FromBase64String(Password)));
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            if (obj is EncryptedNetworkCredential other)
            {
                return UserName.Equals(other.UserName) && Password.Equals(other.Password);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode() => (UserName + Password).GetHashCode();
    }
}
