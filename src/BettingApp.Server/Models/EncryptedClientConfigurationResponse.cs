namespace BettingApp.Server.Models;

public sealed record EncryptedClientConfigurationResponse(
    string Nonce,
    string CipherText,
    string Tag,
    string Algorithm,
    string KeyVersion);
