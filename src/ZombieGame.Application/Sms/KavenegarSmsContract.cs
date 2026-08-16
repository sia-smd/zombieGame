namespace ZombieGame.Application.Sms;

using ZombieGame.Application.Options;

public static class KavenegarSmsContract
{
    public static string ToReceptor(string mobileNumber)
    {
        var number = mobileNumber.Trim().TrimStart('+');
        if (number.StartsWith("98", StringComparison.Ordinal) && number.Length >= 12)
            return "0" + number[2..];
        return number;
    }

    public static string BuildRequestPath(KavenegarSmsSettings settings)
    {
        var method = string.IsNullOrWhiteSpace(settings.Template) ? "sms/send" : "verify/lookup";
        return $"{settings.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(settings.ApiKey)}/{method}.json";
    }

    public static Dictionary<string, string> BuildForm(KavenegarSmsSettings settings, string mobileNumber, string code)
    {
        var receptor = ToReceptor(mobileNumber);
        if (!string.IsNullOrWhiteSpace(settings.Template))
        {
            return new Dictionary<string, string>
            {
                ["receptor"] = receptor,
                ["token"] = code,
                ["template"] = settings.Template
            };
        }

        var form = new Dictionary<string, string>
        {
            ["receptor"] = receptor,
            ["message"] = $"Zombie Game code: {code}"
        };
        if (!string.IsNullOrWhiteSpace(settings.Sender))
            form["sender"] = settings.Sender;
        return form;
    }

    public static bool IsSuccessStatus(int status) => status is >= 200 and < 300;
}
