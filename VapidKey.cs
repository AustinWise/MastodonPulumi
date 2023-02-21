using Pulumi;
using System;
using System.Security.Cryptography;

namespace MastodonPulumi
{
    class VapidKey
    {
        const byte POINT_CONVERSION_UNCOMPRESSED = 4;

        public const string PUB_SECRET_NAME = "VapidPublicKey";
        public const string PRIV_SECRET_NAME = "VapidPrivateKey";

        public static (string pubKey, string privKey) GenKeys()
        {
            var ecdsa = ECDsa.Create();
            ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256);
            return ToStr(ecdsa.ExportParameters(true));
        }

        public VapidKey(Config cfg)
        {
            try
            {
                PublicKey = cfg.RequireSecret(PUB_SECRET_NAME);
                PrivateKey = cfg.RequireSecret(PRIV_SECRET_NAME);
            }
            catch (Exception ex)
            {
                throw new Exception("Vapid keys are not run, run `dotnet run gen-vapid` to get instructions for setting the keys.", ex);
            }
        }

        public Output<string> PublicKey { get; }
        public Output<string> PrivateKey { get; }


        static ECParameters FromStr(string pubKeyStr, string privKeyStr)
        {
            var publicBytes = decode64(pubKeyStr);
            var privteBytes = decode64(privKeyStr);

            if (publicBytes.Length != 65)
                throw new ArgumentOutOfRangeException(nameof(pubKeyStr), "Expected 65 bytes after bbase64 decoding");
            if (privteBytes.Length != 32)
                throw new ArgumentOutOfRangeException(nameof(privKeyStr), "Expected 32 bytes after bbase64 decoding");

            if (publicBytes[0] != POINT_CONVERSION_UNCOMPRESSED)
                throw new Exception("Expected POINT_CONVERSION_UNCOMPRESSED");

            return new ECParameters()
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = privteBytes,
                Q = new ECPoint()
                {
                    X = publicBytes.AsSpan().Slice(1, 32).ToArray(),
                    Y = publicBytes.AsSpan().Slice(33, 32).ToArray(),
                },
            };
        }

        static (string pubKey, string privKey) ToStr(ECParameters ecParams)
        {
            if (ecParams.D.Length != 32)
                throw new ArgumentException("Expected 32 bytes of D");
            if (ecParams.Q.X.Length != 32)
                throw new Exception("Expected 32 bytes of Q.X");
            if (ecParams.Q.Y.Length != 32)
                throw new Exception("Expected 32 bytes of Q.Y");

            byte[] pubBytes = new byte[65];
            pubBytes[0] = POINT_CONVERSION_UNCOMPRESSED;
            ecParams.Q.X.CopyTo(pubBytes.AsSpan().Slice(1, 32));
            ecParams.Q.Y.CopyTo(pubBytes.AsSpan().Slice(33, 32));

            string pubKey = encode64(pubBytes);
            string privKey = encode64(ecParams.D);
            return (pubKey, privKey);
        }

        static string encode64(byte[] input)
        {
            return Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_');
        }

        static byte[] decode64(string input)
        {
            return Convert.FromBase64String(input.Replace('-', '+').Replace('_', '/'));
        }
    }
}
