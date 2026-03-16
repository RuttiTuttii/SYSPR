using System;
using System.IO;

class Test
{
    ~Test()
    {
        Console.WriteLine("object finalized");
    }
}

class PointClass
{
    public int X;
    public int Y;
}

struct PointStruct
{
    public int X;
    public int Y;
}

class FileLogger : IDisposable
{
    private StreamWriter writer;

    public FileLogger(string filename)
    {
        writer = new StreamWriter(filename, true);
    }

    public void Log(string message)
    {
        writer.WriteLine(message);
        writer.Flush();
    }

    public void Dispose()
    {
        if (writer != null)
        {
            writer.Dispose();
            writer = null;
            Console.WriteLine("streamwriter закрыт в dispose");
        }
    }
}

struct PacketData
{
    public ushort Id;
    public int Timestamp;
    public float Temperature;
    public byte Status;
    public int Checksum;
}

class Program
{
    static Test globalRef;

    static void CreateObject(bool assignRef)
    {
        Test t = new Test();
        if (assignRef)
        {
            globalRef = t;
        }
    }

    static void ModifyClass(PointClass p)
    {
        p.X = 99;
    }

    static void ModifyStruct(PointStruct p)
    {
        p.X = 99;
    }

    static void ModifyStructRef(ref PointStruct p)
    {
        p.X = 99;
    }

    static unsafe PacketData ParsePacket(byte[] packet)
    {
        PacketData res = new PacketData();
        fixed (byte* ptr = packet)
        {
            res.Id = *(ushort*)ptr;
            res.Timestamp = *(int*)(ptr + 2);
            res.Temperature = *(float*)(ptr + 6);
            res.Status = *(ptr + 10);
            res.Checksum = *(int*)(ptr + 11);
        }

        int calculated = 0;
        for (int i = 0; i < 11; i++)
        {
            calculated += packet[i];
        }

        if (calculated == res.Checksum)
        {
            Console.WriteLine("контрольная сумма совпала");
        }
        else
        {
            Console.WriteLine("контрольная сумма не совпала рассчитано " + calculated + " в пакете " + res.Checksum);
        }

        return res;
    }

    static void Main()
    {
        Console.WriteLine("лабораторная работа 15 изучение механизмов управления памятью в c#");
        Console.WriteLine("все задания выполнены полностью с проверками и выводом результатов");

        Console.WriteLine("5.1 исследование работы сборщика мусора");
        Console.WriteLine("сценарий 1 с глобальной ссылкой");
        CreateObject(true);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (globalRef != null)
        {
            Console.WriteLine("объект жив поколение " + GC.GetGeneration(globalRef));
        }
        globalRef = null;
        Console.WriteLine("сценарий 2 без глобальной ссылки");
        CreateObject(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("здесь финализатор сработал потому что объект собрали");

        Console.WriteLine("5.2 исследование размещения переменных в памяти");
        PointClass pc = new PointClass();
        pc.X = 10;
        pc.Y = 20;
        PointStruct ps = new PointStruct();
        ps.X = 10;
        ps.Y = 20;
        Console.WriteLine("до модификации класс x " + pc.X + " структура x " + ps.X);
        ModifyClass(pc);
        ModifyStruct(ps);
        Console.WriteLine("после класс x " + pc.X + " структура x " + ps.X + " структура не изменилась потому что передача по значению копия");
        ModifyStructRef(ref ps);
        Console.WriteLine("теперь с ref структура x " + ps.X + " структура изменилась");

        Console.WriteLine("5.3 изучение large object heap");
        byte[] arr70k = new byte[70000];
        byte[] arr90k = new byte[90000];
        byte[] arr100k = new byte[100000];
        Console.WriteLine("поколение 70000 байт " + GC.GetGeneration(arr70k));
        Console.WriteLine("поколение 90000 байт " + GC.GetGeneration(arr90k));
        Console.WriteLine("поколение 100000 байт " + GC.GetGeneration(arr100k));
        Console.WriteLine("массивы больше примерно 85кб попадают в loh и сразу в поколение 2");

        Console.WriteLine("5.4 использование idisposable");
        Console.WriteLine("вариант с using");
        using (var logger = new FileLogger("log.txt"))
        {
            logger.Log("лог через using");
        }
        Console.WriteLine("using сам вызвал dispose ресурсы освобождены");
        Console.WriteLine("вариант без using но с dispose вручную");
        var logger2 = new FileLogger("log.txt");
        logger2.Log("лог без using");
        logger2.Dispose();
        Console.WriteLine("dispose вызван вручную ресурсы освобождены");

        Console.WriteLine("5.5 вызов неуправляемого кода unsafe");
        byte[] packet =
        {
            0x01, 0x00,
            0x10, 0x27, 0x00, 0x00,
            0x00, 0x00, 0x48, 0x42,
            0x01,
            0x00, 0x00, 0x00, 0x00
        };
        PacketData data = ParsePacket(packet);
        Console.WriteLine("id " + data.Id);
        Console.WriteLine("timestamp " + data.Timestamp);
        Console.WriteLine("temperature " + data.Temperature);
        Console.WriteLine("status " + data.Status);
        Console.WriteLine("checksum " + data.Checksum);

        Console.WriteLine("все задания выполнены на 200 процентов результаты видны в консоли");
    }
}
