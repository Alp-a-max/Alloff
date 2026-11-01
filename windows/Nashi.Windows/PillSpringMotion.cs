using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nashi.Windows;

// The input window keeps its final size. A noninteractive glass layer uses independent
// render transforms so the visual spring never moves the target underneath a click.
internal static class PillSpringMotion
{
    internal const double Damping=.82;
    internal static double Response(double seconds,double stiffness)
    {
        var frequency=Math.Sqrt(Math.Clamp(stiffness,100,600));var damped=frequency*Math.Sqrt(1-Damping*Damping);
        return 1-Math.Exp(-Damping*frequency*Math.Max(0,seconds))*(Math.Cos(damped*seconds)+Damping/Math.Sqrt(1-Damping*Damping)*Math.Sin(damped*seconds));
    }
    internal static TimeSpan Duration(double stiffness)=>TimeSpan.FromMilliseconds(Math.Clamp(6000/(Math.Sqrt(Math.Clamp(stiffness,100,600))*Damping),220,450));
    internal static void Animate(ScaleTransform transform,TranslateTransform position,double fromX,double fromY,double offsetY,double stiffness,Action completed)
    {
        var duration=Duration(stiffness);transform.BeginAnimation(ScaleTransform.ScaleXProperty,null);transform.BeginAnimation(ScaleTransform.ScaleYProperty,null);position.BeginAnimation(TranslateTransform.YProperty,null);transform.ScaleX=transform.ScaleY=1;position.Y=0;
        DoubleAnimationUsingKeyFrames Motion(double initial,double target,double frequency)
        {
            var animation=new DoubleAnimationUsingKeyFrames{Duration=new Duration(duration)};
            for(var frame=0;frame<=24;frame++){var fraction=frame/24d;var response=frame==24?1:Response(duration.TotalSeconds*fraction,frequency);animation.KeyFrames.Add(new LinearDoubleKeyFrame(initial+(target-initial)*response,KeyTime.FromPercent(fraction)));}
            return animation;
        }
        var x=Motion(fromX,1,stiffness*.9);var y=Motion(fromY,1,stiffness*1.1);var offset=Motion(offsetY,0,stiffness*1.2);y.Completed+=(_,_)=>completed();transform.BeginAnimation(ScaleTransform.ScaleXProperty,x);transform.BeginAnimation(ScaleTransform.ScaleYProperty,y);position.BeginAnimation(TranslateTransform.YProperty,offset);
    }
}
