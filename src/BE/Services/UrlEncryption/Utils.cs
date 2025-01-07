﻿using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace Chats.BE.Services.UrlEncryption;

internal class Utils
{
    public static byte[] GenerateIdHasherKey(string idHasherPassword, int keyLength, int iterations)
    {
        // Parameters for PBKDF2
        byte[] salt = new byte[16];
        using Rfc2898DeriveBytes rfc2898DeriveBytes = new(idHasherPassword, salt, iterations, HashAlgorithmName.SHA256);
        return rfc2898DeriveBytes.GetBytes(keyLength);
    }

    internal static string SignData(byte[] cleanBytes, byte[] key)
    {
        byte[] output = HMACSHA256.HashData(key, cleanBytes);
        return WebEncoders.Base64UrlEncode(output);
    }

    /// <summary>
    /// The encrypted data is structured as base64url([1:version + encryptedData])
    /// </summary>
    public static string Encrypt(ReadOnlySpan<byte> input, byte[] key, byte[] iv)
    {
        using Aes aes = Aes.Create();
        aes.Key = key;

        byte[] encryptedIdBytes = aes.EncryptCbc(input, iv);
        return WebEncoders.Base64UrlEncode(encryptedIdBytes);
    }

    public static byte[] Decrypt(string encrypted, byte[] key, byte[] iv)
    {
        byte[] encryptedIdBytes = WebEncoders.Base64UrlDecode(encrypted);

        if (encryptedIdBytes.Length != 16)
        {
            throw new InvalidOperationException("Invalid encrypted ID length.");
        }

        using Aes aes = Aes.Create();
        aes.Key = key;
        byte[] decryptedIdBytes = aes.DecryptCbc(encryptedIdBytes, iv);

        return decryptedIdBytes;
    }
}
