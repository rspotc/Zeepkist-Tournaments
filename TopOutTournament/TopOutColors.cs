using UnityEngine;

public static class TopOutColors
{
    public static Color colorFailure = new Color32(0xD9, 0x26, 0x33, 0xFF);
    public static Color colorNeutral = new Color32(0x1B, 0xB3, 0xFF, 0xFF);
    public static Color colorSuccess = new Color32(0x66, 0xA6, 0x00, 0xFF);
    public static Color colorOffWhite = new Color32(0xF1, 0xE6, 0xD9, 0xFF);
    public static Color colorText = Color.white;

    public static Color colorWinner = colorSuccess;
    public static Color colorFinalist = new Color32(0xFF, 0xBF, 0x00, 0xFF);
    public static Color colorPlayer = colorNeutral;
    public static Color colorNuisance = colorFailure;
    public static Color colorReset = Color.white;

    public static Color colorTimer = Color.white;

    public static string convertColor(Color color)
    {
        return $"<#{ColorUtility.ToHtmlStringRGB(color)}>";
    }
}
