namespace ZombieGame.Application.Validation;

using ZombieGame.Application.Common;

public static class AvatarImageValidator
{
    public const int MaxBytes = 300_000;

    public static byte[] DecodeAndValidate(string imageBase64)
    {
        if (string.IsNullOrWhiteSpace(imageBase64))
            throw new ServiceException("Avatar image is required.");

        var payload = imageBase64.Trim();
        var comma = payload.IndexOf(',');
        if (comma >= 0)
            payload = payload[(comma + 1)..];

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw new ServiceException("Avatar image is not valid base64.");
        }

        if (bytes.Length == 0)
            throw new ServiceException("Avatar image is empty.");

        if (bytes.Length > MaxBytes)
            throw new ServiceException("Avatar image is too large.");

        if (!IsSupportedImage(bytes))
            throw new ServiceException("Avatar must be a JPEG, PNG, or WebP image.");

        return bytes;
    }

    private static bool IsSupportedImage(byte[] bytes) =>
        bytes.Length >= 12 && (
            (bytes[0] == 0xFF && bytes[1] == 0xD8) ||
            (bytes[0] == 0x89 && bytes[1] == 0x50) ||
            (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46));
}
