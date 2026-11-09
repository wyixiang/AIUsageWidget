using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
namespace CodexTaskbar;
internal static class ProductIcons
{
    private static readonly Dictionary<string,Geometry[]> Shapes = new();
    internal static FrameworkElement Create(bool cursor, System.Windows.Media.Brush stroke)
    {
        var name=cursor?"mouse-pointer-2":"terminal";
        if(!Shapes.TryGetValue(name,out var shapes))
        {
            using var stream=System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/assets/icons/{name}.svg"))!.Stream;
            var doc=XDocument.Load(stream);
            shapes=doc.Root!.Elements().Where(e=>e.Name.LocalName=="path").Select(e=>Geometry.Parse(e.Attribute("d")!.Value)).ToArray();
            foreach(var shape in shapes) shape.Freeze();
            Shapes[name]=shapes;
        }
        var canvas=new Canvas { Width=24,Height=24 };
        foreach(var shape in shapes) canvas.Children.Add(new System.Windows.Shapes.Path { Data=shape,Stroke=stroke,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round });
        return new Viewbox { Width=16,Height=16,Stretch=Stretch.Uniform,Child=canvas,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false };
    }
}
