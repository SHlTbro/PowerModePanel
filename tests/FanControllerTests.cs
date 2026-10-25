using System;
using System.Reflection;
using System.Windows.Forms;

class FanControllerTests
{
    static int passes;
    static readonly byte[] Profile = Hex("01-10-00-00-08-06-02-02-00-00-00-00-00-00-00-00-2D-2E-2F-64-3F-5F-28-34-63-64-3C-50-03-00-01-00-72-08-2D-2E-2F-64-3F-63-28-42-63-64-3C-50-02-00-01-00-72-08-00-64-64-64-01-02-00-64-64-64-00-00-29-00-00-00-00-00-00-00");
    static readonly byte[] Saved = Hex("00-00-34-75-63-78-42-75-63-78-64-FF-64-FF-00-03-00-01-08-72-00-02-00-01-08-72-00-29-00-00-00-00");
    static byte[] Hex(string text) { byte[] b=new byte[256]; string[] s=text.Split('-'); for(int i=0;i<s.Length;i++) b[i]=Convert.ToByte(s[i],16); return b; }
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
    static void Check(string name,Action action) { action(); passes++; Console.WriteLine("PASS "+name); }
    static bool Same(byte[] a,byte[] b) { for(int i=0;i<a.Length;i++) if(a[i]!=b[i])return false; return true; }
    class Hardware
    {
        public byte[] Settings=(byte[])Profile.Clone(), Curve=(byte[])Saved.Clone();
        public byte[] Firmware=Hex("00-00-00-00-C8-00-00-C8-00-00-C8-00-02-00-04-06-28-3F-34-75-63-78-64-FF-28-3F-42-75-63-78-63-FF-00-00-64-FF-64-FF");
        public byte Fan=6, Overall=2, CpuTemperature=70;
        public bool FailRead, FailPersistence, RejectNext, IgnoreCurve;
        public int Writes;
        public byte[] Read(int page,int index,int length)
        {
            if(FailRead) throw new InvalidOperationException("driver unavailable");
            byte[] b=new byte[length];
            if(page==4) Array.Copy(Settings,index,b,0,length);
            else if(page==1 && index==1 && length==1) b[0]=Overall;
            else throw new Exception("Unexpected settings read");
            return b;
        }
        public byte[] Package(int command)
        {
            if(FailRead) throw new InvalidOperationException("driver unavailable");
            if(command==13) return (byte[])Firmware.Clone();
            if(command==12) { byte[] b=new byte[256]; b[18]=CpuTemperature; b[21]=63; return b; }
            throw new Exception("Unexpected firmware query");
        }
        public int Command(int command,byte[] b)
        {
            Writes++;
            if(RejectNext) { RejectNext=false; return unchecked((int)0x80000002); }
            if(command==14)
            {
                Assert(b.Length==256,"OEM curve packet length");
                if(!IgnoreCurve) { Curve=(byte[])b.Clone(); for(int fan=0;fan<3;fan++) Array.Copy(b,2+fan*4,Firmware,18+fan*8,4); }
            }
            else
            {
                Assert(command==121 && b.Length==4 && b[1]==0 && b[2]==0 && b[3]==1,"Fan action wrote a performance command");
                Fan=b[0];
            }
            return command;
        }
        public void Persist(int page,int index,byte value)
        {
            Assert(page==4 && index==5,"Fan action wrote a performance setting");
            if(FailPersistence) { FailPersistence=false; throw new InvalidOperationException("settings write failed"); }
            Settings[5]=value;
        }
    }
    static object Controller(Hardware h)
    {
        Type t=typeof(PowerModePanel.FanController);
        ConstructorInfo ctor=t.GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,
            new Type[]{typeof(Func<int,byte[],int>),typeof(Func<int,int,int,byte[]>),typeof(Action<int,int,byte>),typeof(Func<int,byte[]>)},null);
        if(ctor!=null) return ctor.Invoke(new object[]{new Func<int,byte[],int>(h.Command),new Func<int,int,int,byte[]>(h.Read),new Action<int,int,byte>(h.Persist),new Func<int,byte[]>(h.Package)});
        // Exercise the old production behavior too: its missing curve write is the original RED.
        ctor=t.GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new Type[]{typeof(Func<int,byte[],int>),typeof(Func<int,int,byte>),typeof(Action<int,int,byte>)},null);
        return ctor.Invoke(new object[]{new Func<int,byte[],int>(h.Command),new Func<int,int,byte>(delegate(int p,int i){return h.Read(p,i,1)[0];}),new Action<int,int,byte>(h.Persist)});
    }
    static void Invoke(object c,string name) { try { c.GetType().GetMethod(name).Invoke(c,null); } catch(TargetInvocationException e) { throw e.InnerException; } }
    static bool IsQuiet(object c) { return (bool)c.GetType().GetMethod("IsQuiet").Invoke(c,null); }
    static bool Fails(object c,string method) { try { Invoke(c,method); return false; } catch(InvalidOperationException) { return true; } }
    [STAThread] static int Main()
    {
        try
        {
            Check("quiet installs low-temperature curve with full-speed hot endpoints",delegate {
                Hardware h=new Hardware(); object c=Controller(h); Invoke(c,"SetQuiet");
                Assert(h.Curve[2]==75 && h.Curve[3]==64 && h.Curve[4]==85 && h.Curve[5]==255,"RED: quiet did not install the verified 25 percent CPU curve");
                Assert(h.Curve[6]==70 && h.Curve[7]==64 && h.Curve[8]==80 && h.Curve[9]==255,"GPU curve missing thermal endpoint");
                Assert(h.Curve[14]==0 && h.Curve[15]==0 && h.Curve[16]==1 && h.Curve[17]==50,"CPU ramp does not use the firmware minimum and OEM slope units");
                Assert(h.Fan==6 && h.Settings[5]==6 && h.Overall==2 && IsQuiet(c),"quiet activation or readback failed");
                Assert(Same(h.Settings,Profile),"quiet overwrote the user's saved fan curve");
                for(int i=10;i<14;i++) Assert(h.Curve[i]==Saved[i],"inactive fan curve was overwritten");
            });
            Check("automatic restores saved curve and its exact cached slopes",delegate {
                Hardware h=new Hardware(); object c=Controller(h); Invoke(c,"SetQuiet"); Invoke(c,"SetAutomatic");
                Assert(Same(h.Curve,Saved) && h.Fan==0 && h.Settings[5]==0 && h.Overall==2 && !IsQuiet(c),"automatic failed to restore user curve and regulation");
            });
            Check("partial quiet failure restores original custom curve",delegate {
                Hardware h=new Hardware(); h.FailPersistence=true; object c=Controller(h);
                Assert(Fails(c,"SetQuiet") && Same(h.Curve,Saved) && Same(h.Settings,Profile) && h.Fan==6,"partial change was not rolled back");
            });
            Check("failed automatic change restores active quiet curve",delegate {
                Hardware h=new Hardware(); object c=Controller(h); Invoke(c,"SetQuiet"); byte[] quiet=(byte[])h.Curve.Clone(); h.FailPersistence=true;
                Assert(Fails(c,"SetAutomatic") && Same(h.Curve,quiet) && h.Fan==6 && IsQuiet(c),"automatic failure lost the previous quiet curve");
            });
            Check("rejected command is not reported as success",delegate {
                Hardware h=new Hardware(); h.RejectNext=true; Assert(Fails(Controller(h),"SetQuiet") && Same(h.Curve,Saved) && Same(h.Settings,Profile),"rejected command escaped error handling");
            });
            Check("read failure produces no writes",delegate {
                Hardware h=new Hardware(); h.FailRead=true; Assert(Fails(Controller(h),"SetQuiet") && h.Writes==0,"read failure still changed hardware");
            });
            Check("unsupported fan layout produces no writes",delegate {
                Hardware h=new Hardware(); h.Firmware[12]=1; Assert(Fails(Controller(h),"SetQuiet") && h.Writes==0,"unknown layout was modified");
            });
            Check("hot machine is not switched to reduced cooling",delegate {
                Hardware h=new Hardware(); h.CpuTemperature=85; Assert(Fails(Controller(h),"SetQuiet") && h.Writes==0,"hot-machine guard missing");
            });
            Check("firmware that ignores the curve cannot fake success",delegate {
                Hardware h=new Hardware(); h.IgnoreCurve=true; Assert(Fails(Controller(h),"SetQuiet"),"settings echo was mistaken for firmware acknowledgement");
            });
            Check("hardware minimum above 29 percent is respected",delegate {
                Hardware h=new Hardware(); h.Firmware[17]=100; Assert(Fails(Controller(h),"SetQuiet") && h.Writes==0,"curve fell below hardware minimum");
            });
            Check("both UI buttons reach real fan controller",delegate {
                Hardware h=new Hardware(); object c=Controller(h);
                using(PowerModePanel.MainForm f=new PowerModePanel.MainForm())
                {
                    typeof(PowerModePanel.MainForm).GetField("fanController",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,c);
                    MethodInfo click=typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic);
                    foreach(string name in new string[]{"fanQuiet","fanAutomatic"})
                    {
                        Control b=f.Controls[name]; Assert(b is Button,"missing fan button"); click.Invoke(b,new object[]{EventArgs.Empty});
                        Assert(name=="fanQuiet" ? IsQuiet(c) : h.Fan==0,"button wiring failed");
                    }
                }
            });
            Check("50 percent saved duty restores as OEM raw value 128",delegate {
                Hardware h=new Hardware(); h.Settings[17]=50; h.Settings[18]=60;
                Invoke(Controller(h),"SetAutomatic");
                Assert(h.Curve[3]==128,"OEM percent conversion rounded 50 percent down to 127");
            });
            Check("failed upgrade preserves the active legacy 29 percent quiet curve",delegate {
                Hardware h=new Hardware();
                byte[] legacy=Hex("00-00-4B-4A-55-FF-46-4A-50-FF-64-FF-64-FF-00-05-01-22-00-00-00-05-01-22-00-00-00-29-00-00-00-00");
                h.Command(14,legacy); h.FailPersistence=true; object c=Controller(h);
                Assert(Fails(c,"SetQuiet") && Same(h.Curve,legacy) && IsQuiet(c),"failed upgrade changed the previous quiet curve");
            });
            Check("25 percent target never crosses a higher firmware minimum",delegate {
                Hardware h=new Hardware(); h.Firmware[17]=70;
                Assert(Fails(Controller(h),"SetQuiet") && h.Writes==0,"new curve crossed a minimum between 25 and 29 percent");
            });
            Console.WriteLine(passes+" checks passed"); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e.Message); return 1; }
    }
}
