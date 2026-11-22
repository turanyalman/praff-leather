using Microsoft.AspNetCore.Html;

namespace PraffLeather.Services;

/// <summary>Arayüzde kullanılan çizgi ikonlar (tasarım paketindeki SVG'ler).</summary>
public static class Icon
{
    private static HtmlString S(string paths, int size = 20, string cls = "ico") =>
        new($"<svg class=\"{cls}\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">{paths}</svg>");

    public static HtmlString Bag(int s = 20) => S("<path d=\"M5 8h14l-1 12H6L5 8z\"/><path d=\"M9 8V6a3 3 0 0 1 6 0v2\"/>", s);
    public static HtmlString Cart(int s = 22) => S("<path d=\"M3 4h3l2 11h10l2-8H7\"/><circle cx=\"9\" cy=\"19\" r=\"1.4\"/><circle cx=\"17\" cy=\"19\" r=\"1.4\"/>", s);
    public static HtmlString Box(int s = 22) => S("<path d=\"M12 3l8 4.5v9L12 21l-8-4.5v-9z\"/><path d=\"M4 7.5l8 4.5 8-4.5M12 12v9\"/>", s);
    public static HtmlString User(int s = 22) => S("<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 21c1-4.5 4-6 8-6s7 1.5 8 6\"/>", s);
    public static HtmlString Pin(int s = 22) => S("<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.5\"/>", s);
    public static HtmlString Gear(int s = 22) => S("<circle cx=\"12\" cy=\"12\" r=\"3\"/><path d=\"M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2 2M16.4 16.4l2 2M5.6 18.4l2-2M16.4 7.6l2-2\"/>", s);
    public static HtmlString Lock(int s = 22) => S("<rect x=\"5\" y=\"11\" width=\"14\" height=\"9\" rx=\"2\"/><path d=\"M8 11V8a4 4 0 0 1 8 0v3\"/>", s);
    public static HtmlString Logout(int s = 22) => S("<path d=\"M10 4H5v16h5M15 8l4 4-4 4M19 12H9\"/>", s);
    public static HtmlString Mail(int s = 20) => S("<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"M3 7l9 6 9-6\"/>", s);
    public static HtmlString Phone(int s = 20) => S("<path d=\"M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2z\"/>", s);
    public static HtmlString Eye(int s = 20) => S("<path d=\"M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>", s);
    public static HtmlString Search(int s = 22) => S("<circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4 4\"/>", s);
    public static HtmlString Zoom(int s = 20) => S("<circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4 4M11 8v6M8 11h6\"/>", s);
    public static HtmlString Trash(int s = 22) => S("<path d=\"M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6\"/>", s);
    public static HtmlString Back(int s = 20) => S("<path d=\"M19 12H5M11 6l-6 6 6 6\"/>", s);
    public static HtmlString Home(int s = 20) => S("<path d=\"M4 11l8-7 8 7v9H4z\"/>", s);
    public static HtmlString Grid(int s = 20) => S("<rect x=\"4\" y=\"4\" width=\"7\" height=\"7\"/><rect x=\"13\" y=\"4\" width=\"7\" height=\"7\"/><rect x=\"4\" y=\"13\" width=\"7\" height=\"7\"/><rect x=\"13\" y=\"13\" width=\"7\" height=\"7\"/>", s);
    public static HtmlString List(int s = 20) => S("<path d=\"M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01\"/>", s);
    public static HtmlString Chat(int s = 20) => S("<path d=\"M4 5h16v11H9l-5 4z\"/>", s);
    public static HtmlString Chevron(int s = 18) => S("<path d=\"M9 5l7 7-7 7\"/>", s);
    public static HtmlString ChevronL(int s = 18) => S("<path d=\"M15 5l-7 7 7 7\"/>", s);
    public static HtmlString Menu(int s = 22) => S("<path d=\"M4 7h16M4 12h16M4 17h16\"/>", s);
    public static HtmlString Send(int s = 20) => S("<path d=\"M21 3L3 10l7 3 3 7z\"/><path d=\"M10 13l11-10\"/>", s);
    public static HtmlString Arrow(int s = 18) => S("<path d=\"M5 12h14M13 6l6 6-6 6\"/>", s);
    public static HtmlString Instagram(int s = 20) => S("<rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"5\"/><circle cx=\"12\" cy=\"12\" r=\"4\"/><circle cx=\"17\" cy=\"7\" r=\".6\"/>", s);
    public static HtmlString WhatsApp(int s = 20) => S("<path d=\"M4 20l1.5-4.2A8 8 0 1 1 8.2 18.6z\"/><path d=\"M9 8.5c0 3.5 3 6.5 6.5 6.5l1-1.5-2-1-1 .8c-1-.4-2-1.4-2.4-2.4l.8-1-1-2z\"/>", s);
    public static HtmlString Youtube(int s = 20) => S("<rect x=\"2.5\" y=\"5.5\" width=\"19\" height=\"13\" rx=\"4\"/><path d=\"M10 9l5 3-5 3z\"/>", s);
    public static HtmlString Linkedin(int s = 20) => S("<rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"3\"/><path d=\"M8 11v5M8 8v.01M12 16v-5M12 13c0-2 4-2.5 4 0v3\"/>", s);
    public static HtmlString Tiktok(int s = 20) => S("<path d=\"M14 4v10.5a3.5 3.5 0 1 1-3.5-3.5M14 4c.5 2.5 2 4 5 4\"/>", s);
    public static HtmlString Clock(int s = 20) => S("<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/>", s);
    public static HtmlString Up(int s = 20) => S("<path d=\"M12 19V6M6 11l6-6 6 6\"/>", s);
    public static HtmlString Layers(int s = 22) => S("<path d=\"M12 3l9 5-9 5-9-5zM3 13l9 5 9-5M3 17l9 5 9-5\"/>", s);
}
