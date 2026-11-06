using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace CodexTaskbar;

internal static class CursorSessionDecoder
{
    internal static string Decode(string stored)
    {
        const string prefix="cursor-safe-storage:v1:";
        if(!stored.StartsWith(prefix,StringComparison.Ordinal)) return stored;
        byte[]? protectedToken=null,key=null,plaintext=null;
        try
        {
            protectedToken=Convert.FromBase64String(stored[prefix.Length..]);
            if(protectedToken.AsSpan().StartsWith("v10"u8))
            {
                var statePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Cursor","Local State");
                using var state=JsonDocument.Parse(File.ReadAllText(statePath));
                var protectedKey=Convert.FromBase64String(state.RootElement.GetProperty("os_crypt").GetProperty("encrypted_key").GetString()!);
                try
                {
                    if(!protectedKey.AsSpan().StartsWith("DPAPI"u8)) throw new CryptographicException();
                    key=Unprotect(protectedKey[5..]);
                }
                finally { CryptographicOperations.ZeroMemory(protectedKey); }
                if(protectedToken.Length<31) throw new CryptographicException();
                plaintext=new byte[protectedToken.Length-3-12-16];
                using var aes=new AesGcm(key,16);
                aes.Decrypt(protectedToken.AsSpan(3,12),protectedToken.AsSpan(15,plaintext.Length),protectedToken.AsSpan(protectedToken.Length-16),plaintext);
            }
            else plaintext=Unprotect(protectedToken);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch(Exception ex) when(ex is CryptographicException or JsonException or FormatException or IOException or KeyNotFoundException)
        { throw new InvalidOperationException("无法读取 Cursor 的加密登录状态"); }
        finally
        {
            if(protectedToken is not null) CryptographicOperations.ZeroMemory(protectedToken);
            if(key is not null) CryptographicOperations.ZeroMemory(key);
            if(plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }
    private static byte[] Unprotect(byte[] data)
    {
        var pinned=GCHandle.Alloc(data,GCHandleType.Pinned);
        var input=new Blob { Length=data.Length,Data=pinned.AddrOfPinnedObject() }; Blob output=default;
        try
        {
            if(!CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output)) throw new CryptographicException();
            var result=new byte[output.Length]; Marshal.Copy(output.Data,result,0,result.Length); return result;
        }
        finally
        {
            pinned.Free();
            if(output.Data!=IntPtr.Zero)
            {
                for(var i=0;i<output.Length;i++) Marshal.WriteByte(output.Data,i,0);
                LocalFree(output.Data);
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { internal int Length; internal IntPtr Data; }
    [DllImport("crypt32.dll",SetLastError=true)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
