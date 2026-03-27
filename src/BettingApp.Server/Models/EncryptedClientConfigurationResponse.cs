namespace BettingApp.Server.Models;

public sealed record EncryptedClientConfigurationResponse(
    string ConfigId,
    int ConfigVersion,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    string Nonce,
    string CipherText,
    string Tag,
    string Algorithm,
    string KeyVersion,
    string Signature);
