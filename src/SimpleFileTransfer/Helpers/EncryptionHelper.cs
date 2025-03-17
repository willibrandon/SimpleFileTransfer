using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SimpleFileTransfer.Helpers;

/// <summary>
/// Provides encryption and decryption functionality for file transfers.
/// Uses AES encryption with password-based key derivation.
/// </summary>
public static class EncryptionHelper
{
    // Use a fixed salt for simplicity
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("SimpleFileTransferSalt");
    
    // Number of iterations for key derivation
    private const int Iterations = 10000;
    
    // Key size in bits
    private const int KeySize = 256;
    
    /// <summary>
    /// Encrypts data from the source stream and writes it to the destination stream.
    /// </summary>
    /// <param name="source">The stream containing data to encrypt.</param>
    /// <param name="destination">The stream to write encrypted data to.</param>
    /// <param name="password">The password to use for encryption.</param>
    public static void Encrypt(Stream source, Stream destination, string password)
    {
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        
        // Derive key from password
        using var deriveBytes = new Rfc2898DeriveBytes(password, Salt, Iterations, HashAlgorithmName.SHA256);
        aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
        
        // Generate a random IV and write it to the output stream
        aes.GenerateIV();
        destination.Write(aes.IV, 0, aes.IV.Length);
        
        // Create encryptor and encrypt the data
        using var encryptor = aes.CreateEncryptor();
        using var cryptoStream = new CryptoStream(destination, encryptor, CryptoStreamMode.Write);
        
        // Copy the source data to the crypto stream
        source.CopyTo(cryptoStream);
        cryptoStream.FlushFinalBlock();
    }
    
    /// <summary>
    /// Decrypts data from the source stream and writes it to the destination stream.
    /// </summary>
    /// <param name="source">The stream containing encrypted data.</param>
    /// <param name="destination">The stream to write decrypted data to.</param>
    /// <param name="password">The password to use for decryption.</param>
    /// <returns>True if decryption was successful, false otherwise.</returns>
    public static bool Decrypt(Stream source, Stream destination, string password)
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            
            // Derive key from password
            using var deriveBytes = new Rfc2898DeriveBytes(password, Salt, Iterations, HashAlgorithmName.SHA256);
            aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
            
            // Read the IV from the beginning of the encrypted data
            byte[] iv = new byte[aes.IV.Length];
            int bytesRead = source.Read(iv, 0, iv.Length);
            if (bytesRead < iv.Length)
            {
                throw new InvalidDataException("Encrypted data is too short or corrupted.");
            }
            aes.IV = iv;
            
            // Create decryptor and decrypt the data
            using var decryptor = aes.CreateDecryptor();
            using var cryptoStream = new CryptoStream(source, decryptor, CryptoStreamMode.Read);
            
            // Copy the decrypted data to the destination stream
            cryptoStream.CopyTo(destination);
            
            return true;
        }
        catch (CryptographicException)
        {
            // Decryption failed, likely due to wrong password
            SafeConsoleWrite("Decryption failed. The password may be incorrect.");
            return false;
        }
        catch (Exception ex)
        {
            SafeConsoleWrite($"Error during decryption: {ex.Message}");
            return false;
        }
    }
    
    private static void SafeConsoleWrite(string message)
    {
        try
        {
            Console.WriteLine(message);
        }
        catch (ObjectDisposedException)
        {
            // Ignore exception when console is closed
        }
    }
}
