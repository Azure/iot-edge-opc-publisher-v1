// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace OpcPublisher
{
    /// <summary>
    /// Provides in-memory access to encrypted credentials. It uses the OPC UA app cert to encrypt/decrypt credentials.
    /// </summary>
    public class EncryptedNetworkCredential : NetworkCredential
    {
        public static EncryptedNetworkCredential Encrypt(X509Certificate2 cert, NetworkCredential networkCredential)
        {
            EncryptedNetworkCredential result = new EncryptedNetworkCredential();

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

        public NetworkCredential Decrypt(X509Certificate2 cert)
        {
            NetworkCredential result = new NetworkCredential();

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
