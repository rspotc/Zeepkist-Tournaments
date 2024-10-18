using UnityEngine;

public static class TopOutColors
{
    public static Color colorFailure = new Color32(0xA8, 0x3E, 0x48, 0xFF);
    public static Color colorNeutral = new Color32(0x57, 0x69, 0xA1, 0xFF);
    public static Color colorSuccess = new Color32(0x58, 0x7B, 0x4B, 0xFF);
    public static Color colorText = new Color32(0xF1, 0xE6, 0xD9, 0xFF);

    public static Color colorWinner = colorSuccess;
    public static Color colorFinalist = Color.yellow;
    public static Color colorPlayer = colorNeutral;
    public static Color colorNuisance = colorFailure;
    public static Color colorReset = Color.white;

    public static Color colorTimer = Color.white;

    public static string convertColor(Color color)
    {
        return $"<#{ColorUtility.ToHtmlStringRGB(color)}>";
    }
}
