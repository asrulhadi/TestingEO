using DynamicData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TestingEO.Models;

public class Pelco
{
    /// Original code from https://gist.github.com/jn0/cc5c78f4a0f447a6fb2e45a5d9efa13d
    /// Original code in Python

    // Manual mapping
    public readonly Dictionary<string, MethodInfo> str2procA = new();
    public readonly Dictionary<string, MethodInfo> str2procB = new();
    public readonly Dictionary<string, MethodInfo> str2procC = new();
    public Action<Task> FunctionListReady = _ => { };
    public Pelco()
    {
    }

    public Task StartPopulate()
    {
        return Task.Run(() => PopulateFunction()).ContinueWith(FunctionListReady);
    }
    private void PopulateFunction()
    {
        // autocreation
        Type t = GetType();
        foreach (MethodInfo mi in t.GetMethods())
        {
            var nama = mi.Name.Replace('_', ' ');
            var paramsInfo = mi.GetParameters();
            var noOfArgs = paramsInfo.Length;
            if (noOfArgs == 0) continue;

            if (paramsInfo[0].Name == "addr")
            {   // our pelco methods
                (Dictionary<string, MethodInfo>? addTo, string loc) = noOfArgs switch
                {
                    1 => (str2procA, nameof(str2procA)),
                    2 when paramsInfo[1].ParameterType == typeof(bool) => (str2procB, nameof(str2procB)),
                    2 when paramsInfo[1].ParameterType == typeof(int) => (str2procC, nameof(str2procC)),
                    _ => (null, "null")
                };
                if (addTo is null) Debug.WriteLine($"Tak tahu nak tambah {mi.Name} kat mana");
                if (loc == "str2procC") Console.WriteLine("Adding to {1}: {0} => {2}", mi.Name, loc, nama);
                addTo?.Add(nama, mi);
            }
        }
    }

    /* The Pelco-D Protocol

    Standard Number                                                     TF-0002
    Version         2       Revision    1       Release Date    August 15, 2003

    Transmitters will format a single character and receivers will be able to
    decipher a single character as: 1 start bit, 8 data bits, 1 stop bit,
    and no parity.

    Intended use:

        import serial
        import pelco_d

        CAM = 1

        with serial.Serial(PORT) as com:
            command, parser = pelco_d.get(pelco_d.CameraOn, CAM)
            com.write(command)
            response = com.read()
            print parser(response, command[-1])

    THE MESSAGE FORMAT

    The format for a message is:

    Byte 1      Byte 2  Byte 3      Byte 4      Byte 5  Byte 6  Byte 7
    Sync Byte   Address Command 1   Command 2   Data 1  Data 2  Checksum

    Note that values in this document prefixed with “0x” are hexadecimal numbers.
    The synchronization byte (Sync Byte) is always 0xFF.
    The Address is the logical address of the receiver/driver device being controlled.
    The Checksum is calculated by performing the 8 bit (modulo 256) sum of the
    payload bytes(bytes 2 through 6) in the message.
    */

    public (byte[], Func<string, byte, byte[]>) Unknown(string proc)
    {
        Debug.WriteLine("Function <{0}> tak dijumpai", proc);
        byte[] r = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0xBA, 0xBE };
        return (r, (_, _) => r);
    }
    public (byte[], Func<string, byte, byte[]>) get(string proc, int addr)
    {
        if (!str2procA.ContainsKey(proc)) return Unknown(proc);
        Debug.WriteLine("Command called: {0} with addr={1}", proc, addr);
        byte[] command = (byte[])str2procA[proc]!.Invoke(this, new object[] { addr }) ?? new byte[] { };
        return (command, Parse_General_Response);
    }

    public (byte[], Func<string, byte, byte[]>) get(string proc, int addr, int data)
    {
        if (!str2procC.ContainsKey(proc)) return Unknown(proc);
        Debug.WriteLine("Command called: {0} with addr={1} data={2}", proc, addr, data);
        byte[] command = (byte[])str2procC[proc]!.Invoke(this, new object[] { addr, data }) ?? new byte[] { };
        return (command, Parse_General_Response);
    }
    public (byte[], Func<string, byte, byte[]>) get(string proc, int addr, bool cond)
    {
        if (!str2procB.ContainsKey(proc)) return Unknown(proc);
        Debug.WriteLine("Command called: {0} with addr={1} cond={2}", proc, addr, cond);
        byte[] command = (byte[])str2procB[proc]!.Invoke(this, new object[] { addr, cond }) ?? new byte[] { };
        return (command, Parse_General_Response);
    }

    private byte SYNC = 0xff;

    private byte[] command(int addr, int cmd, int data = 0)
    {
        Debug.Assert((0 <= addr) && (addr <= 255), $"Address out of range: {addr}");
        Debug.Assert((0 <= cmd) && (cmd <= 65535), $"Command out of range: {cmd}");
        Debug.Assert((0 <= data) && (data <= 65535), $"Data out of range: {data}");
        byte ad = (byte)addr;
        byte c1 = (byte)((cmd >> 8) & 255);
        byte c2 = (byte)(cmd & 255);
        byte d1 = (byte)((data >> 8) & 255);
        byte d2 = (byte)(data & 255);
        return command(ad, c1, c2, d1, d2);
    }
    private byte[] command(params byte[] av)
    {
        Debug.Assert(av.Length == 5, $"Parameters must be 5. Obtained {av}");
        Debug.Assert(av.All(x => (0 <= x) && (x <= 255)), "Value must be 0<x<255");
        byte checksum = (byte)(av.Sum(x => x) & 255);
        //return bytearray((SYNC,) + tuple(av) + (checksum,));
        List<byte> result = new();
        result.Add(SYNC);
        result.AddRange(av);
        result.Add(checksum);
        return result.ToArray();
    }

    #region Standard Command
    /* STANDARD COMMANDS

    Command 1 and 2 are represented as follows:
              Bit 7 Bit 6   Bit 5   Bit 4   Bit 3   Bit 2   Bit 1   Bit 0
    Command 1 Sense Rsrvd   Rsrvd   Auto/   Camera  Iris    Iris    Focus
                                    Manual  On      Close   Open    Near
                                    Scan    Off
    Command 2 Focus Zoom    Zoom    Down    Up      Left    Right   Always 0
              Far   Wide    Tele

    A value of ‘1’ entered in the bit location for the function desired will enable
    that function.A value of ‘0’ entered in the same bit location will disable or
    ‘stop’ the function.

    The sense bit (command 1 bit 7) indicates the meaning of bits 4 and 3.
    If the sense bit is on(value of ‘1’), and bits 4 and 3 are on,
    the command will enable auto-scan and turn the camera on.
    If the sense bit is off (value of ‘0’), and bits 4 and 3 are on the command
    will enable manual scan and turn the camera off.
    Of course, if either bit 4 or bit 3 are off then no action will be taken for
    those features.

    The reserved bits (6 and 5) should be set to 0.

    Byte 5 contains the pan speed.
    Pan speed is in the range of ‘0x00’ to ‘0x3F’ (high speed) and ‘0x40’ for “turbo” speed.
    Turbo speed is the maximum speed the device can obtain and is considered
    separately because it is not generally a smooth step from high speed to turbo.
    That is, going from one speed to the next usually looks smooth and will provide
    for smooth motion with the exception of going into and out of turbo speed.A pan
    speed value of ‘0x00’ results in very slow motion, not cessation of motion.
    To stop pan motion both the Left and Right direction bits must be turned off
    – set to ‘0’ – regardless of the value set in the pan speed byte.

    Byte 6 contains the tilt speed.
    Tilt speed is in the range of ‘0x00’ to ‘0x3F’ (maximum speed).
    Turbo speed is not allowed for the tilt axis.
    A tilt speed value of ‘0x00’ results in very slow motion, not cessation of motion.
    To stop tilt motion both the Down and Up direction bits must be turned off
    – set to ‘0’ – regardless of the value set in the tilt speed byte.

    Byte 7 is the checksum.
    The checksum is the 8 bit (modulo 256) sum of the payload bytes(bytes 2 through
    6) in the message.
    */
    #region Standard Command Bits
    //                      76543210
    private byte SENSE = 0b10000000;
    private byte RSRV6 = 0b01000000;
    private byte RSRV5 = 0b00100000;
    private byte SCAN = 0b00010000;
    private byte CAMERA = 0b00001000;
    private byte IRISCL = 0b00000100;
    private byte IRISOP = 0b00000010;
    private byte FOCUSN = 0b00000001;

    private byte FOCUSF = 0b10000000;
    private byte ZOOMWD = 0b01000000;
    private byte ZOOMTL = 0b00100000;
    private byte DOWN = 0b00010000;
    private byte UP = 0b00001000;
    private byte LEFT = 0b00000100;
    private byte RIGHT = 0b00000010;
    private byte RSRV0 = 0b00000001;

    private ushort FAKE_PAN_SPEED = 0x1200;
    private ushort FAKE_TILT_SPEED = 0x0012;
    #endregion

    public byte[] Standard_Command(int addr, int cmd1, int cmd2, ushort data = 0)
    {
        // General Response
        Debug.Assert((0 <= addr) && (addr <= 255), $"Address is out of range: {addr}");
        Debug.Assert((0 <= cmd1) && (cmd1 <= 255), $"Command is out of range: {cmd1}");
        Debug.Assert((cmd1 & (RSRV6 | RSRV5)) == 0, $"Not a standard command: {cmd1}");
        Debug.Assert((0 <= cmd2) && (cmd2 <= 255), $"Command is out of range: {cmd2}");
        Debug.Assert((cmd2 & RSRV0) == 0, $"Not a standard command: {cmd2}");
        Debug.Assert((0 <= data) && (data <= 65535), $"Data is out of range: {data}");
        // d1, d2 = (data >> 8) & 255, data & 255
        return command((byte)addr, (byte)cmd1, (byte)cmd2, (byte)((data >> 8) & 255), (byte)(data & 255));
    }

    public byte[] Reset(int addr)
    {
        //'General Reponse'
        return Standard_Command(addr, 0, 0);
    }
    public byte[] Camera_On(int addr)
    {
        //'General Reponse'
        return Standard_Command(addr, SENSE | CAMERA, 0);
    }
    public byte[] Camera_Off(int addr)
    {
        //'General Reponse'
        return Standard_Command(addr, CAMERA, 0);
    }
    public byte[] Camera(int addr, bool on)
    {
        //'General Reponse'
        return on ? Camera_On(addr) : Camera_Off(addr);
    }

    public byte[] Scan_Auto(int addr)
    {
        //'General Reponse'
        return Standard_Command(addr, SENSE | SCAN, 0);
    }
    public byte[] Scan_Manual(int addr)
    {
        //'General Reponse'
        return Standard_Command(addr, SCAN, 0);
    }
    public byte[] Scan(int addr, bool auto)
    {
        //'General Reponse'
        return auto ? Scan_Auto(addr) : Scan_Manual(addr);
    }

    public byte[] Iris_Close(int addr)
    {
        //'General Response'
        return Standard_Command(addr, IRISCL, 0);
    }
    public byte[] Iris_Open(int addr)
    {
        //'General Response'
        return Standard_Command(addr, IRISOP, 0);
    }
    public byte[] Iris(int addr, bool open)
    {
        //'General Response'
        return open ? Iris_Open(addr) : Iris_Close(addr);
    }

    public byte[] Focus_Near(int addr)
    {
        //'General Response'
        return Standard_Command(addr, FOCUSN, 0);
    }
    public byte[] Focus_Far(int addr)
    {
        //'General Response'
        return Standard_Command(addr, 0, FOCUSF);
    }
    public byte[] Focus(int addr, bool far)
    {
        //'General Response'
        return far ? Focus_Far(addr) : Focus_Near(addr);
    }

    public byte[] Zoom_Wide(int addr)
    {
        //'General Response'
        return Standard_Command(addr, 0, ZOOMWD);
    }
    public byte[] Zoom_Tele(int addr)
    {
        //'General Response'
        return Standard_Command(addr, 0, ZOOMTL);
    }
    public byte[] Zoom(int addr, bool tele)
    {
        //'General Response'
        return tele ? Zoom_Tele(addr) : Zoom_Wide(addr);
    }

    public byte[] Pan_Left(int addr, int speed)
    {
        //'General Response'
        Debug.Assert((0 <= speed) && (speed <= 0x40), $"Speed out of range: {speed}");
        return Standard_Command(addr, 0, LEFT, (ushort)(speed << 8));
    }

    public byte[] Pan_Right(int addr, int speed)
    {
        //'General Response'
        Debug.Assert((0 <= speed) && (speed <= 0x40), $"Speed out of range: {speed}");
        return Standard_Command(addr, 0, RIGHT, (ushort)(speed << 8));
    }

    public byte[] Pan_Stop(int addr)
    {
        //'General Response'
        return Standard_Command(addr, 0, 0, FAKE_PAN_SPEED);
    }

    public byte[] Pan(int addr, int speed)
    {
        //'General Response'
        return speed switch
        {
            var p when p > 0 => Pan_Right(addr, speed),
            var n when n < 0 => Pan_Left(addr, -speed),
            _ => Pan_Stop(addr),
        };
    }

    public byte[] Tilt_Up(int addr, int speed)
    {
        //'General Response'
        Debug.Assert((0 <= speed) && (speed <= 0x3f), $"Speed out of range {speed}");
        return Standard_Command(addr, 0, UP, (ushort)speed);
    }
    public byte[] Tilt_Down(int addr, int speed)
    {
        //'General Response'
        Debug.Assert((0 <= speed) && (speed <= 0x3f), $"Speed out of range {speed}");
        return Standard_Command(addr, 0, DOWN, (ushort)speed);
    }

    public byte[] Tilt_Stop(int addr)
    {
        //'General Response'
        return Standard_Command(addr, 0, 0, FAKE_TILT_SPEED);
    }

    public byte[] Tilt(int addr, int speed)
    {
        //'General Response'
        return speed switch
        {
            var p when p > 0 => Tilt_Up(addr, speed),
            var n when n < 0 => Tilt_Down(addr, -speed),
            _ => Tilt_Stop(addr),
        };
    }
    #endregion

    /* PRESET
    Set Preset(0x03)
    Clear Preset(0x05)
    Go To Preset(0x07)

    The parameter in byte 6 of these commands is the ID of the preset to be acted on.
    Valid preset IDs begin at 1.
    Most devices support at least 32 presets.
    Refer to the manual of the device under use for information about
    what range of presets are valid for that equipment.
    */
    /* Preset
    AUTO, ON, OFF = 0, 1, 2

    public byte[] Set_Preset(addr, presetId)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= presetId <= 255, presetId
        return command(addr, 0, 0x03, 0, presetId)

    public byte[] Clear_Preset(addr, presetId)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= presetId <= 255, presetId
        return command(addr, 0, 0x05, 0, presetId)

    public byte[] Go_To_Preset(addr, presetId)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= presetId <= 255, presetId
        return command(addr, 0, 0x07, 0, presetId)

    public byte[] Flip_180_about(int addr)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x07, 0, 0x21)

    public byte[] Go_To_Zero_Pan(int addr)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x07, 0, 0x22)

    public byte[] Set_Auxiliary(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= value <= 8, value
        return command(addr, 0, 0x09, 0, value)

    public byte[] Clear_Auxiliary(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= value <= 8, value
        return command(addr, 0, 0x0b, 0, value)

    public byte[] Remote_Reset(int addr)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x0f, 0, 0)

    public byte[] Set_Zone_Start(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= value <= 8, value
        return command(addr, 0, 0x11, 0, value)

    public byte[] Set_Zone_End(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= value <= 8, value
        return command(addr, 0, 0x13, 0, value)
    */

    /* WRITE TO SCREEN
    Write Character To Screen(0x15)
    The parameter in byte 5 of this command indicates the column to write to.
    This parameter is interpreted as follows:
    - Columns 0-19 are used to receive zone labels.
    - Columns 20-39 are used to receive preset labels.
    */
    /* Write to Screen
    public byte[] Write_Character_to_Screen(addr, column, char)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= column <= 39, column
        Debug.Assert 0 <= char <= 255, char
        return command(addr, 0, 0x15, column, char)

    public byte[] Write_Zone_Label(addr, column, char)
    {
		//'General Response'
        Debug.Assert 0 <= column <= 19, column
        return Write_Character_to_Screen(addr, column, char)

    public byte[] Write_Preset_Lebel(addr, column, char)
    {
		//'General Response'
        Debug.Assert 0 <= column <= 19, column
        return Write_Character_to_Screen(addr, 20 + column, char)


    public byte[] Clear_Screen(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x17, 0, 0)

    public byte[] Alarm_Acknowledge(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 1 <= value <= 8, value
        return command(addr, 0, 0x19, 0, value)

    public byte[] Zone_Scan_On(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x1b, 0, 0)

    public byte[] Zone_Scan_Off(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x1d, 0, 0)

    __doc__ += '''
    Set Pattern Start(0x1F)
    Run Pattern(0x23)

    The parameter in byte 6 of these commands indicates the pattern to be set/run.

    Platform dependent.
    '''

    public byte[] Set_Pattern_Start(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 255, value
        return command(addr, 0, 0x1f, 0, value)

    public byte[] Set_Pattern_Stop(int addr)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x21, 0, 0)

    public byte[] Run_Pattern(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 255, value
        return command(addr, 0, 0x23, 0, value)

    public byte[] Set_Zoom_Speed(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 3, value
        return command(addr, 0, 0x25, 0, value)

    public byte[] Set_Focus_Speed(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 3, value
        return command(addr, 0, 0x27, 0, value)

    public byte[] Reset_Camera_to_defaults(int addr)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x29, 0, 0)

    public byte[] Auto_focus(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert value in (AUTO, ON, OFF), value
        return command(addr, 0, 0x2b, 0, value)

    public byte[] Auto_Iris(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert value in (AUTO, ON, OFF), value
        return command(addr, 0, 0x2d, 0, value)

    public byte[] AGC(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert value in (AUTO, ON, OFF), value
        return command(addr, 0, 0x2f, 0, value)

    public byte[] Backlight_compensation(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert value in (ON, OFF), value
        return command(addr, 0, 0x31, 0, value)

    public byte[] Auto_white_balance(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert value in (ON, OFF), value
        return command(addr, 0, 0x33, 0, value)

    public byte[] Enable_device_phase_delay_mode(int addr)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0, 0x35, 0, 0)

    public byte[] Set_shutter_speed(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x37, d1, d2)

    public byte[] Adjust_line_lock_phase_delay_0(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x39, d1, d2)

    public byte[] Adjust_line_lock_phase_delay_1(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 1, 0x39, d1, d2)

    public byte[] Adjust_white_balance_RB0(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x3b, d1, d2)

    public byte[] Adjust_white_balance_RB1(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 1, 0x3b, d1, d2)

    public byte[] Adjust_white_balance_MG0(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x3d, d1, d2)

    public byte[] Adjust_white_balance_MG1(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 1, 0x3d, d1, d2)

    public byte[] Adjust_gain_0(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x3f, d1, d2)

    public byte[] Adjust_gain_1(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 1, 0x3f, d1, d2)

    public byte[] Adjust_auto_iris_level_0(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x41, d1, d2)

    public byte[] Adjust_auto_iris_level_1(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 1, 0x41, d1, d2)

    public byte[] Adjust_auto_iris_peak_0(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x43, d1, d2)

    public byte[] Adjust_auto_iris_peak_1(addr, value)
    {
		//'General Response'
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 1, 0x43, d1, d2)

    public byte[] Query(addr, value)
        '''This command can only be used in a point to point application.
        A device being queried will respond to any address.
        If more than one device hears this command,
        multiple devices will transmit at the same time.

        Query45 Response'''

        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 65535, value
        d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0, 0x45, d1, d2)

    public byte[] Reserved_Opcode(addr, value)
        Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert 0 <= value <= 255, addr
        return command(addr, 0, value, 0, 0)

    public byte[] Reserved_Opcode_47(int addr) return Reserved_Opcode(addr, 0x47)
    */

    #region Extended Command
    /* Set Zero Position(0x49)

    This command is used to set the pan position that the unit uses as a zero
    reference point for the azimuth on-screen display.
    The unit’s current pan position when this command is received becomes the zero
    reference point.
    This command performs the same function as the “Set Azimuth Zero” menu item.
    */
    public byte[] Set_Zero_Position(int addr)
    {
        //'General Response'
        //Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0x49, 0);
    }

    /* Set Pan Position(0x4B)

    This command is used to set the pan position of the device.
    The position is given in hundredths of a degree and has a range from 0 to 35999
    (decimal).
    Example: the value to use to set the pan position to 45 degrees is 4500.

    Note that the value used here is always the “absolute” pan position.
    It does not take into account any adjustment to the screen display that may
    have been made by using the “Set Zero Position”, opcode(0x49) command or
    the “Set Azimuth Zero” menu item.
    '''
    */
    public byte[] Set_Pan_Position(int addr, int degrees)
    {
        //'General Response'
        //Debug.Assert((0 <= addr) && (addr <= 255), $"{addr}");
        Debug.Assert((0 <= degrees) && (degrees <= 35999), $"Degree out of range: {degrees}");
        //d1, d2 = (degrees >> 8) & 255, degrees & 255
        return command(addr, 0x4b, degrees);
    }

    /* Set Tilt Position(0x4D)

    This command is used to set the tilt position of the device.
    The position is given in hundredths of a degree and has a range from 0 to 35999
    (decimal). Generally these values are interpreted as follows:
    - Zero degrees indicates that the device is pointed horizontally(at the horizon).
    - Ninety degrees indicates that the device is pointed straight down.
    Examples:
    1) the value used to set the tilt position to 45 degrees below the horizon, is 4500.
    2) the value used to set the tilt position 30 degrees above the horizon, is 33000.

    Note that different equipment will have different ranges of motion.
    To determine the abilities of a specific piece of equipment,
    refer to that device’s operation manual.
    */
    private ushort TILT_NONE = 0;
    private ushort TITL_HORIZON = 0;
    private ushort TILT_DOWN = 9000;
    private ushort TILT_UP = 27000;

    public byte[] Set_Tilt_Position(int addr, int degrees)
    {
        //'General Response'
        //Debug.Assert((0 <= addr) && (addr <= 255), $"{addr}");
        Debug.Assert((0 <= degrees) && (degrees <= 35999), $"Degree out of range: {degrees}");
        //d1, d2 = (degrees >> 8) & 255, degrees & 255
        return command(addr, 0x4d, degrees);
    }

    /* Set Zoom Position(0x4F)

    This command is used to set the zoom position of the device.
    The position is given as a ratio based on the device’s Zoom Limit setting.
    The position is calculated as follows:

        Position = (desired_zoom_position / zoom_limit) * 65535

    Where desired_zoom_position and zoom_limit are given in units of magnification.

    Example:
        Given that the zoom limit of the device’s camera is X184,
        calculate the value needed to set the zoom position to X5:

        Position = (5 / 184) * 65535 = approximately 1781
    */
    public byte[] Set_Zoom_Position(int addr, int value)
    {
        //'General Response'
        //Debug.Assert((0 <= addr) && (addr <= 255), $"{addr}");
        //Debug.Assert((0 <= value) && (value <= 65535), $"Value out of range: {value}");
        //d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0x4f, value);
    }

    /* Query Pan Position(0x51)

    This command is used to query the current pan position of the device.
    The response to this command uses opcode 0x59.
    See the description of opcode 0x59 for more information.
    */
    public byte[] Query_Pan_Position(int addr)
    {
        //'Extended Response'
        //Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0x51);
    }

    /* Query Tilt Position(0x53)

    This command is used to query the current tilt position of the device.
    The response to this command uses opcode 0x5B.
    See the description of opcode 0x5B for more information.
    */
    public byte[] Query_Tilt_Position(int addr)
    {
        //'Extended Response'
        //Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0x53);
    }

    /* Query Zoom Position(0x55)

    This command is used to query the current zoom position of the device.
    The response to this command uses opcode 0x5D.
    See the description of opcode 0x5D for more information.
    */
    public byte[] Query_Zoom_Position(int addr)
    {
        //'Extended Response'
        //Debug.Assert 0 <= addr <= 255, addr
        return command(addr, 0x55);
    }


    /* Query Pan Position Response(0x59)

    The position is given in hundredths of a degree and has a range from 0 to 35999 (decimal).

    Example: a position value of 4500 indicates 45 degrees.

    Note that the value returned is always the “absolute” pan position.
    It does not take into account any adjustment to the screen display that may
    have been made by using the “Set Zero Position”, opcode(0x49) command or
    the “Set Azimuth Zero” menu item.
    */
    public byte[] Query_Pan_Response(int addr, int degrees)
    {
        //'Extended Response' # ??? XXX
        //Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert((0 <= degrees) && (degrees <= 35999), $"Degree out of range: {degrees}");
        //d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0x59, degrees);
    }

    /* Query Tilt Position Response(0x5B)

    The position is given in hundredths of a degree and has a range from 0 to 35999 (decimal).
    Refer to examples listed in description of the “Set Tilt Position”, opcode 0x4D command.
    */
    public byte[] Query_Tilt_Response(int addr, int degrees)
    {
        //'Extended Response' # ??? XXX
        //Debug.Assert 0 <= addr <= 255, addr
        Debug.Assert((0 <= degrees) && (degrees <= 35999), $"Degree out of range: {degrees}");
        //d1, d2 = (value >> 8) & 255, value & 255
        return command(addr, 0x5b, degrees);
    }

    public byte[] Query_Zoom_Response(int addr, int value)
    {
        //'Extended Response' # ??? XXX
        return command(addr, 0x5d, value);
    }

    /* Set Magnification(0x5F)

    This command is used to set the zoom position of the device.
    The position is given in hundredths of units of magnification.
    Example: a value of 500 means X5.
    */
    public byte[] Set_Magnification(int addr, int value)
    {
        //'General Response'
        return command(addr, 0x5f, value);
    }

    /* Query Magnification(0x61)

    This command is used to query the current zoom position of the device.
    The response to this command uses opcode 0x63.
    See the description of opcode 0x63 for more information.
    */
    public byte[] Query_Magnification(int addr)
    {
        //'Extended Response'
        return command(addr, 0x61);
    }

    /* Query Magnification Response(0x63)

    The value returned is given in hundredths of units of magnification.
    Example: a value of 500 means X5.
    */
    public byte[] Query_Magnification_Response(int addr, int value)
    {
        return command(addr, 0x63, value);
    }
    #endregion

    #region Reserved Opcode
    public byte[] Reserved_Opcode_57(int addr) => Reserved_Opcode(addr, 0x57);
    public byte[] Reserved_Opcode_65(int addr) => Reserved_Opcode(addr, 0x65);
    public byte[] Reserved_Opcode_67(int addr) => Reserved_Opcode(addr, 0x67);
    public byte[] Reserved_Opcode_69(int addr) => Reserved_Opcode(addr, 0x69);
    public byte[] Reserved_Opcode_6b(int addr) => Reserved_Opcode(addr, 0x6b);
    public byte[] Reserved_Opcode_6d(int addr) => Reserved_Opcode(addr, 0x6d);
    public byte[] Reserved_Opcode_6f(int addr) => Reserved_Opcode(addr, 0x6f);
    public byte[] Reserved_Opcode_71(int addr) => Reserved_Opcode(addr, 0x71);
    public byte[] Reserved_Opcode(int addr, int opcode) => command(addr, opcode);
    #endregion

    /* The General Response - 4 bytes

    The General Response has the following format.
    Note that each block represents 1 byte.

    Byte 1  Byte 2   Byte 3              Byte 4
    Sync    Address  Alarm Information   Checksum

    The alarm information is formatted as follows:
    Bit 7  Bit 6    Bit 5    Bit 4    Bit 3    Bit 2    Bit 1    Bit 0
    None   Alarm 7  Alarm 6  Alarm 5  Alarm 4  Alarm 3  Alarm 2  Alarm 1

    If the bit is on (1) then the alarm is active.If the bit is off(0)
    then the alarm is inactive.
    The checksum is the sum of the transmitted command’s checksum and the alarm
    information.
    */

    #region Alarm
    //                      76543210
    private byte ALARM1 = 0b00000001;
    private byte ALARM2 = 0b00000010;
    private byte ALARM3 = 0b00000100;
    private byte ALARM4 = 0b00001000;
    private byte ALARM5 = 0b00010000;
    private byte ALARM6 = 0b00100000;
    private byte ALARM7 = 0b01000000;
    private byte ALARMX = 0b10000000;
    #endregion

    public byte[] Parse_General_Response(string resp, byte sent_cs = 0)
    {
        Debug.Assert(resp.Length == 4, $"Length is {resp}");
        Debug.Assert(resp[0] == SYNC, $"Response does not start with FF");
        byte addr = Convert.ToByte(resp[1]);
        byte info = (byte)(Convert.ToByte(resp[2]) & ~ALARMX);
        Debug.Assert((0 <= info) && (info <= 127), $"Info out of range: {info}");
        if (sent_cs != 0)
        {
            Debug.Assert((0 <= sent_cs) && (sent_cs <= 255), $" Checksum not valid: {sent_cs}");
            byte csum = Convert.ToByte(resp[3]);
            Debug.Assert(csum == ((sent_cs + info) & 255), $"Checksum not match: {resp}");
        }
        byte res = 0;
        if ((info & ALARM1) != 0) res += ALARM1;
        if ((info & ALARM2) != 0) res += ALARM2;
        if ((info & ALARM3) != 0) res += ALARM3;
        if ((info & ALARM4) != 0) res += ALARM4;
        if ((info & ALARM5) != 0) res += ALARM5;
        if ((info & ALARM6) != 0) res += ALARM6;
        if ((info & ALARM7) != 0) res += ALARM7;
        return new byte[] { addr, info };
    }

    /* The Query(0x45) Response - 18 bytes

    The response to the Query command is:
    Byte 1  Byte 2    Bytes 3 to 17           Byte 18
    Sync    Address   Part Number(15 bytes)   Checksum

    The address is the address of the device responding to the query.
    The content of the part number field is dependent on the type and version of
    the device being programmed, please refer to the table that follows.
    The checksum is the 8 bit(modulo 256) sum of the transmitted query command’s
    checksum, the address of the response, and the 15-byte part number.
    */
    public byte[] Parse_Query45_Response(string resp, byte sent_cs = 0)
    {
        Debug.Assert(resp.Length == 18, $"Not a valid response: {resp}");
        Debug.Assert(resp[0] == SYNC, $"Does not start with SYNC: {resp[0]}");
        byte addr = (byte)resp[1];
        var pnum = resp[2..^1];
        if (sent_cs != 0)
        {
            Debug.Assert((0 <= sent_cs) && (sent_cs <= 255), $"Checksum out of range: {sent_cs}");
            byte csum = (byte)resp[^1];
            byte xsum = (byte)((sent_cs + addr + pnum.Sum(x => x)) & 255);
            Debug.Assert(csum == xsum, $"Incorrect checksum: {csum} {xsum}");
        }
        byte[] ret = new byte[] { addr };
        ret.AddRange(pnum.Cast<byte>());
        return ret;
    }

    /* The Extended Response - 7 bytes

    The Extended Response has the following format.
    Note that each block represents 1 byte

    Byte 1  Byte 2   Byte 3      Byte 4    Byte 5  Byte 6  Byte 7
    Sync    Address  Future Use  “opcode”  Data1   Data2   Checksum

    The address is the address of the device that is responding.
    The Future Use byte should always be set to 0.
    Opcode, Data1 and Data2 are dependent on the type of response.
    See the opcode description section of this document for the details of a particular response.

    The checksum is the 8 bit (modulo 256) sum of all the bytes excluding the Sync byte.
    */
    public byte[] Parse_Extended_Response(string resp, int? a = null, int? op = null)
    {
        Debug.Assert(resp.Length == 7, $"Valid length of Extended Response: {resp}");
        Debug.Assert(resp[0] == SYNC, $"Does not start with SYNC: {resp}");
        byte[] respb = resp.Cast<byte>().ToArray();
        byte addr = respb[1];
        byte rezerved = respb[2];
        Debug.Assert(rezerved == 0, $"Reserved bit should be 0: {rezerved}");
        byte opcode = respb[3];
        if (op is not null) Debug.Assert(opcode == op, $"Different Opcode: {resp}");
        byte data1 = respb[4];
        byte data2 = respb[5];
        byte csum = respb[6];
        byte xsum = (byte)(resp[1..^1].Sum(x => x) & 255);
        Debug.Assert(csum == xsum, $"Incorrect checksum: {csum}");
        return new byte[] { addr, opcode, data1, data2 };
    }

    public Func<string, byte, byte[]> get_parser_for(string docstr)
    {
        //docstr = func.__doc__.strip()

        //if 'Response' in docstr:
        //    for l in docstr.splitlines()
        //        if l.endswith('Response')
        //            if ',' in l:
        //                l, op = l.split(',', 1) # `op` is unused now
        //            parser_name = 'Parse_' + '_'.join(l.split())
        //            if parser_name in globals() :
        //                return globals()[parser_name]
        return Parse_General_Response;
    }
}
