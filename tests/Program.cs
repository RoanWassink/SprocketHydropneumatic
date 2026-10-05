using SprocketHydropneumatic;

int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Near(float a, float b, string message, float tolerance = .00001f) => Check(Math.Abs(a-b) < tolerance, $"{message}: {a} != {b}");
var settings = new HydroSettings { Enabled = true, IndependentControls = true, FrontMin=-.15f,FrontMax=.15f,RearMin=-.15f,RearMax=.15f,Stroke=.4f,Reserve=.02f,Speed=.05f };
var command = new HeightCommands();
var layoutHistory = new PartLayoutHistory<LayoutTest>();
Check(layoutHistory.Begin(false) == null,"Initial standard selection is unchanged");
Check(layoutHistory.Begin(true) == null,"First HPS selection inherits current geometry");
var originalLayout = new LayoutTest(.71f,.15f,7,142,990);
layoutHistory.Remember(originalLayout);
Check(layoutHistory.Begin(false) == null,"Leaving HPS applies no HPS geometry to VVSS");
layoutHistory.Remember(new LayoutTest(.42f,.15f,7,0,700)); // native VVSS cap must not replace HPS memory
Check(layoutHistory.Begin(true) == originalLayout,"Return restores 710mm wheel and forward/spacing values after native VVSS clamp");
Check(layoutHistory.Begin(true) == null,"Ordinary HPS rebuild does not overwrite a manual wheel edit");
var editedLayout = originalLayout with { Diameter=.80f, Forward=210, Count=8 };
layoutHistory.Remember(editedLayout); layoutHistory.Begin(false);
Check(layoutHistory.Begin(true) == editedLayout,"Return retains latest manual HPS edits");
var defaults = new HydroSettings();
Check(defaults.IsValid(),"New HPS defaults valid");
Near(defaults.FrontMax,.1f,"Default raise limit"); Near(defaults.FrontMin,-.1f,"Default lower limit");
Near(defaults.Reserve,.03f,"Default remaining passive travel"); Near(defaults.Speed,.04f,"Default gentle adjustment speed");
var twoSided = new SpringGeometry(.34f,1.66f,.94f,2.16f).HeightRange(defaults.Reserve);
Check(twoSided.Min < -.1f && twoSided.Max > .1f,"Representative HPS arms allow both default lift and lowering");
var tiltSettings = new HydroSettings { Enabled = true, Speed=.05f, FrontMin = -.04f, FrontMax = .06f, RearMin = -.04f, RearMax = .06f };
var tilt = new HeightCommands();
for (int i=0;i<1000;i++) tilt.Step(tiltSettings,0,1,.02f);
Near(tilt.Front,-.04f,"Page Up lowers front to shared stop"); Near(tilt.Rear,.04f,"Page Up raises rear equally");
Near((tilt.Front+tilt.Rear)/2,0,"Saturated tilt adds no whole-tank lift");
for (int i=0;i<1000;i++) tilt.Step(tiltSettings,0,-1,.02f);
Near(tilt.Front,.04f,"Page Down raises front"); Near(tilt.Rear,-.04f,"Page Down lowers rear equally");
tilt.Reset(); for(int i=0;i<1000;i++) tilt.Step(tiltSettings,0,0,.02f);
Near(tilt.Front,0,"Tilt reset front"); Near(tilt.Rear,0,"Tilt reset rear");
for(int i=0;i<20;i++) tilt.Step(tiltSettings,1,0,.02f);
Near(tilt.Front,.02f,"Arrows raise whole tank front"); Near(tilt.Rear,.02f,"Arrows raise whole tank rear");
for(int i=0;i<1000;i++) tilt.Step(tiltSettings,0,1,.02f);
Near(tilt.Front,-.02f,"Tilt at raised ride height front"); Near(tilt.Rear,.06f,"Tilt at raised ride height rear");
Near((tilt.Front+tilt.Rear)/2,.02f,"Tilt preserves raised mean height");
tiltSettings.RearMax=.025f; tilt = new HeightCommands();
for(int i=0;i<1000;i++) tilt.Step(tiltSettings,0,1,.02f);
Near(tilt.Front,-.025f,"Asymmetric stop still equal opposite front"); Near(tilt.Rear,.025f,"Asymmetric rear stop");
Check(!HydroSettings.Parse("{\"Version\":1,\"Enabled\":true}").IndependentControls,"Older saves adopt default tilt mode");
Check(HydroSettings.Parse(settings.Serialize()).IndependentControls,"Independent mode persists");
for (int i=0;i<1000;i++) command.Step(settings, 1, -1, .02f);
Near(command.Front, .15f, "Front clamp"); Near(command.Rear,-.15f,"Rear clamp");
Near(command.At(0),-.15f,"Rear interpolation"); Near(command.At(1),.15f,"Front interpolation"); Near(command.At(.5f),0,"Middle interpolation");
float old = command.Front; command.Step(settings,0,0,5); Near(command.Front,old,"Holding position");
command.Reset(); for(int i=0;i<1000;i++) command.Step(settings,0,0,.02f);
Near(command.Front,0,"Front reset"); Near(command.Rear,0,"Rear reset"); Check(!command.Resetting,"Reset completion");
command.Step(settings,1,0,100); Near(command.Front,.0025f,"No stall catch-up jump");
command.Step(settings,1,0,float.NaN); Near(command.Front,.0025f,"Ignore invalid time");
command.Reset(); command.Step(settings,0,1,.02f); Check(!command.Resetting,"Manual input interrupts reset");
var shortStroke = settings.Copy(); shortStroke.Stroke=.08f;
for(int i=0;i<1000;i++) command.Step(shortStroke,1,-1,.02f);
Near(command.Front,.04f,"Equivalent stroke front"); Near(command.Rear,-.04f,"Equivalent stroke rear");
var positive = new SpringGeometry(.6f, 1.4f, .35f,2.7f);
var mirrored = new SpringGeometry(.6f,-1.4f,-2.7f,-.35f);
Check(positive.Valid && mirrored.Valid,"Valid mirrored arms");
for(int i=-50;i<=50;i++)
{
    float height=i*.005f;
    float a=positive.AngleForHeight(height,.02f), b=mirrored.AngleForHeight(height,.02f);
    Near(a,-b,"Mirrored height symmetry");
    Near(.6f*(MathF.Cos(a)-MathF.Cos(1.4f)),height,"Actual requested wheel travel");
}
var range=positive.HeightRange(.02f);
float hi=positive.AngleForHeight(10,.02f), lo=positive.AngleForHeight(-10,.02f);
Near(.6f*(MathF.Cos(hi)-MathF.Cos(1.4f)),range.Max,"Upper physical stop");
Near(.6f*(MathF.Cos(lo)-MathF.Cos(1.4f)),range.Min,"Lower physical stop");
Check(hi>.35f && lo<2.7f,"Travel reserve preserved");
Near(positive.AngleForHeight(0,.15f),1.4f,"Neutral retained with reserve");
Check(!new SpringGeometry(.6f,0,-1,1).Valid,"Reject nonmonotonic crossed arm branch");
Check(!new SpringGeometry(float.NaN,1,0.5f,2).Valid,"Reject invalid geometry");
// Autosave reproduction: TargetAngle=-60 degrees puts neutral at +/-30
// degrees. HPS's +/-35 degree stops cross zero; retain the useful branch.
float lowArmReference = MathF.PI / 6, hydraulicTravel = 35 * MathF.PI / 180;
var crossing = new SpringGeometry(.34f,lowArmReference,lowArmReference-hydraulicTravel,lowArmReference+hydraulicTravel);
Check(!crossing.Valid,"Raw -60 degree arm crosses cosine turning point");
var constrained = crossing.ConstrainToReferenceBranch();
var reverseConstrained = new SpringGeometry(.34f,-lowArmReference,-lowArmReference-hydraulicTravel,-lowArmReference+hydraulicTravel).ConstrainToReferenceBranch();
Check(constrained.Valid && reverseConstrained.Valid,"Both mirrored -60 degree arms have usable hydraulic branches");
Check(constrained.MinAngle>=crossing.MinAngle && constrained.MaxAngle<=crossing.MaxAngle,"Hydraulic branch cannot extend native stops");
Near(constrained.ReferenceAngle,lowArmReference,"Clipping does not change neutral geometry");
Near(constrained.AngleForHeight(0,.03f),lowArmReference,"Clipped arm preserves neutral stance");
var constrainedRange=constrained.HeightRange(.03f);
Check(constrainedRange.Min<0 && constrainedRange.Max>0,"Clipped arm supports raising and lowering");
for(int i=0;i<=100;i++)
{
    float h=constrainedRange.Min+(constrainedRange.Max-constrainedRange.Min)*i/100;
    float a=constrained.AngleForHeight(h,.03f), b=reverseConstrained.AngleForHeight(h,.03f);
    Near(a,-b,"Clipped mirrored symmetry");
    Near(.34f*(MathF.Cos(a)-MathF.Cos(lowArmReference)),h,"Clipped requested height round trip");
    Check(a>=crossing.MinAngle && a<=crossing.MaxAngle,"Clipped angle respects original native stops");
}
Check(positive.ConstrainToReferenceBranch()==positive,"Ordinary valid geometry unchanged");
Check(!new SpringGeometry(.34f,0,-hydraulicTravel,hydraulicTravel).ConstrainToReferenceBranch().Valid,"Exactly ambiguous neutral remains unsupported");
Check(!new SpringGeometry(.34f,1,2,3).ConstrainToReferenceBranch().Valid,"Branch cannot repair a neutral outside stops");
var fit=HydroSpring.Fit(1.2f,positive,0,1.4f,settings);
Near(fit.RestAngle,1.2f,"Baseline native preload");
var stiff=settings.Copy(); stiff.Stiffness=2;
fit=HydroSpring.Fit(1.2f,positive,0,1.4f,stiff);
Near((fit.RestAngle-1.4f)*fit.StiffnessRatio,1.2f-1.4f,"Preserve neutral linear preload");
var compressed=HydroSpring.Fit(1.2f,positive,0,1.8f,settings);
Check(compressed.StiffnessRatio>1 && compressed.StiffnessRatio<=4,"Bounded progressive response");
for(int i=0;i<1000;i++)
{
    float actual=.35f+(2.7f-.35f)*i/999;
    var p=HydroSpring.Fit(1.2f,positive,.1f,actual,settings);
    var m=HydroSpring.Fit(-1.2f,mirrored,.1f,-actual,settings);
    Near(p.RestAngle,-m.RestAngle,"Mirrored preload response"); Near(p.StiffnessRatio,m.StiffnessRatio,"Mirrored stiffness response");
    Check(float.IsFinite(p.RestAngle) && p.StiffnessRatio>=.25f && p.StiffnessRatio<=4,"Finite progressive model");
}
var roundtrip=HydroSettings.Parse(settings.Serialize()); Check(roundtrip.Enabled,"Persist selected type"); Near(roundtrip.Speed,settings.Speed,"Persist speed");
var copy=settings.Copy(); copy.FrontMax=.1f; Near(settings.FrontMax,.15f,"Independent copied settings");
Check(!new HydroSettings { FrontMax=-.1f }.IsValid(),"Reject reversed front range");
Check(!new HydroSettings { Speed=float.PositiveInfinity }.IsValid(),"Reject infinite speed");
bool rejected=false; try { HydroSettings.Parse("{\"Version\":2}"); } catch(ArgumentException) { rejected=true; }
Check(rejected,"Reject unknown persistence version");
Near(BeltSupport.FrontFraction(-2,-2,2,0,1),0,"Rear support sample");
Near(BeltSupport.FrontFraction(2,-2,2,0,1),1,"Front support sample");
Near(BeltSupport.FrontFraction(-2,-2,2,1,0),1,"Reversed belt front");
Near(BeltSupport.FrontFraction(2,-2,2,1,0),0,"Reversed belt rear");
Near(BeltSupport.FrontFraction(0,-2,2,0,1),.5f,"Middle support sample");
Near(BeltSupport.FrontFraction(8,-2,2,0,1),1,"Support beyond end wheel");
Near(BeltSupport.RestHeight(-.8f,.05f,-.04f,.06f),-.85f,"Raising hull lowers native support Y");
Near(BeltSupport.RestHeight(-.8f,-.03f,-.04f,.06f),-.77f,"Lowering hull raises native support Y");
Near(BeltSupport.RestHeight(-.8f,1,-.04f,.06f),-.86f,"Native support upper stop");
Near(BeltSupport.RestHeight(-.8f,-1,-.04f,.06f),-.76f,"Native support lower stop");
Near(BeltSupport.RestHeight(-.8f,0,-.04f,.06f),-.8f,"Neutral support restored");
Console.WriteLine($"PASS: {checks} geometry, control, progression, belt support and persistence checks. Native gameplay not exercised.");
record LayoutTest(float Diameter, float Width, int Count, int Forward, int Spacing);
