using System;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace RaspiPort
{
    internal class Program
    {
        private static readonly TaskCompletionSource<byte> ShutdownResetEvent = new TaskCompletionSource<byte>();

        private static readonly byte[] numbers = {0x3f, 0x06, 0x5b, 0x4f, 0x66,
                            0x6d, 0x7d, 0x07, 0x7f, 0x6f};

        private static async Task<int> Main(string[] args)
        {
            bool mode = false;

            Pi.Init<BootstrapWiringPi>();//初始化通信接口，分配内存空间等
            var Count = 0;
            var Exit = false;
            _ = Task.Factory.StartNew(() =>
              {
                  var clkPin = Pi.Gpio[BcmPin.Gpio23];//引用16接口
                  var dataPin = Pi.Gpio[BcmPin.Gpio24];//引用18接口
                  clkPin.PinMode = GpioPinDriveMode.Output;//设置16接口模式为输出
                  dataPin.PinMode = GpioPinDriveMode.Output;//设置18接口模式为输出
              init:
                  {
                      Show(0xC0, 0x40);
                      Show(0xC1, 0x40);
                      Show(0xC2, 0x40);
                      Show(0xC3, 0x40);
                  }
                  Console.CancelKeyPress += delegate
                  {
                      startDisp();
                      writeByte(0x80);
                      stopDisp();
                  };
                  void startDisp()
                  {
                      //数据输入开始
                      clkPin.Write(GpioPinValue.High);//CLK拉为高电平
                      dataPin.Write(GpioPinValue.High);//DIO拉为高电平
                      dataPin.Write(GpioPinValue.Low);//和上面那句指令一起就是DIO由高变低
                      clkPin.Write(GpioPinValue.Low);//然后CLK上的时钟信号拉低，DIO接口的数据允许改变，代表开始写入数据
                  }
                  void stopDisp()
                  {
                      //结束条件是CLK为高时，DIO由低电平变为高电平
                      clkPin.Write(GpioPinValue.Low);//先拉低CLK，代表允许DIO改变数据
                      dataPin.Write(GpioPinValue.Low);//拉低DIO
                      clkPin.Write(GpioPinValue.High);//CLK拉高，满足结束条件前半部分
                      dataPin.Write(GpioPinValue.High);//DIO由低变高，代表数据输入结束
                  }
                  void writeByte(byte input)
                  {
                      //开始写入数据
                      for (int i = 0; i < 8; i++)//每次写入一个byte，一共8bit
                      {
                          clkPin.Write(GpioPinValue.Low);//确保无误输入前再拉低一次时钟 ，代表开始写入数据
                          if ((input & 0x01) == 1)//判断每一位的高低电平
                          {
                              dataPin.Write(GpioPinValue.High);
                          }
                          else
                          {
                              dataPin.Write(GpioPinValue.Low);
                          }
                          input >>= 1;//每写入完毕一次，移动一位
                          clkPin.Write(GpioPinValue.High);//每次输入完一位，就拉高一次时钟
                      }
                      //应答信号ACK，这里本来是用来判断DIO脚是否被自动拉低，代表上面写入的数据TM1637已经接受到了，
                      //但是我们这里闲麻烦，直接将CLK信号低高低的拉，让芯片直接执行下一步操作
                      clkPin.Write(GpioPinValue.Low);//先拉低
                      clkPin.Write(GpioPinValue.High);//需要判断D是否为低电平此期间C一直拉高
                      clkPin.Write(GpioPinValue.Low);//应答完毕以后拉低C
                  }
                  void Show(byte address, byte show)
                  {
                      //Start transmission
                      startDisp();//开始写入指令 C高D高D低
                      writeByte(0x44);//40H模式地址自动+1,44H模式固定地址
                      stopDisp();//结束写入指令 C低D低C高D高
                                 //上述完成数据指令设置

                      //设置光标位置为11000000，光标位置每次输入+1
                      startDisp();
                      writeByte(address);//设置首地址
                                         //Write text
                                         //数据从01000000写到01111111
                                         //
                                         //      A
                                         //     ---
                                         //  F |   | B
                                         //     -G-
                                         //  E |   | C
                                         //     ---
                                         //      D
                      /*
                        XGFEDCBA
                        10000000
                        00111111,    // 0
                        00000110,    // 1
                        01011011,    // 2
                        01001111,    // 3
                        01100110,    // 4
                        01101101,    // 5
                        01111101,    // 6
                        00000111,    // 7
                        01111111,    // 8
                        01101111,    // 9
                        01110111,    // A
                        01111100,    // b
                        00111001,    // C
                        01011110,    // d
                        01111001,    // E
                        01110001     // F
                      */
                      //写入字符

                      writeByte(show);
                      //停止写入
                      startDisp();
                      //开始写入亮度
                      startDisp();//开始写入指令
                      writeByte(0x8f);//最大亮度
                      stopDisp(); //停止写入
                  }
                  clkPin.Write(GpioPinValue.High);//初始化电平为低,可不加
                  dataPin.Write(GpioPinValue.High);//初始化电平为低,可不加
                  var ShowCount = false;
                  while (true)
                  {
                      if (mode)
                      {
                          var Date = DateTime.Now.ToString("HHmm").ToCharArray();
                          Show(0xC3, numbers[Date[Date.Length - 1] - 48]);
                          if (Date.Length > 1)
                              Show(0xC2, numbers[Date[Date.Length - 2] - 48]);
                          else Show(0xC2, 0x40);
                          if (Date.Length > 2)
                          {
                              if (ShowCount)
                              {
                                  ShowCount = false;
                                  Show(0xC1, (byte)(numbers[Date[Date.Length - 3] - 48] | 0x80));
                              }
                              else
                              {
                                  ShowCount = true;

                                  Show(0xC1, numbers[Date[Date.Length - 3] - 48]);
                              }
                          }
                          else Show(0xC1, 0x40);
                          if (Date.Length > 3)
                              Show(0xC0, numbers[Date[Date.Length - 4] - 48]);
                          else Show(0xC0, 0x40);
                          //Interlocked.Increment(ref Count);
                          if (Exit)
                          {
                              startDisp();
                              writeByte(0x80);
                              stopDisp();
                              break;
                          }
                          Thread.Sleep(250);
                      }
                      else
                      {
                          var Date = Count.ToString().ToCharArray();
                          Show(0xC3, numbers[Date[Date.Length - 1] - 48]);
                          if (Date.Length > 1)
                              Show(0xC2, numbers[Date[Date.Length - 2] - 48]);
                          else Show(0xC2, 0x40);
                          if (Date.Length > 2)
                              Show(0xC1, numbers[Date[Date.Length - 3] - 48]);
                          else Show(0xC1, 0x40);
                          if (Date.Length > 3)
                              Show(0xC0, numbers[Date[Date.Length - 4] - 48]);
                          else Show(0xC0, 0x40);
                          //Interlocked.Increment(ref Count);
                          if (Count == 10000) Count = 0;
                          if (Exit)
                          {
                              startDisp();
                              writeByte(0x80);
                              stopDisp();
                              break;
                          }
                          Thread.Sleep(100);
                      }
                  }
              });
            _ = Task.Factory.StartNew(() =>
            {
                var Shock = Pi.Gpio[BcmPin.Gpio22];
                Shock.PinMode = GpioPinDriveMode.Input;
                System.Diagnostics.Stopwatch StartWatch = new System.Diagnostics.Stopwatch();
                System.Diagnostics.Stopwatch KeepWatch = new System.Diagnostics.Stopwatch();
                var KeyCount = 0;

                while (true)
                {
                    Thread.Sleep(50);
                    if (!Shock.Read() && !KeepWatch.IsRunning)//按下按钮，计时未开始
                    {
                        KeepWatch.Restart();
                    }
                    else if (Shock.Read() && KeepWatch.IsRunning)//松开按钮，计时开始中，松开按钮计时
                    {
                        KeepWatch.Stop();
                        StartWatch.Restart();
                        KeyCount++;
                    }
                    else if (!Shock.Read() && KeepWatch.IsRunning && KeepWatch.ElapsedMilliseconds > 1000)//按下按钮，计时开始中
                    {
                        if (KeepWatch.ElapsedMilliseconds > 1000)
                        {
                            Count = 0;
                        }
                        else if (KeepWatch.ElapsedMilliseconds > 5000)
                        {
                            Exit = true;
                            Thread.Sleep(100);
                            ShutdownResetEvent.SetResult(0);
                        }
                    }
                    else if (Shock.Read() && KeepWatch.IsRunning && StartWatch.ElapsedMilliseconds > 200)
                    {
                        StartWatch.Restart();
                        KeyCount++;
                    }

                    if (KeyCount != 0 && StartWatch.ElapsedMilliseconds > 500)
                    {
                        switch (KeyCount)
                        {
                            case 1:
                                {
                                    if (Count > 0 && !mode) Interlocked.Decrement(ref Count);
                                }
                                break;

                            case 2:
                                {
                                    if (!mode) Interlocked.Increment(ref Count);
                                }
                                break;

                            default:
                                {
                                    if (KeyCount > 6)
                                        mode = mode ? false : true;
                                }
                                break;
                        }
                        KeepWatch.Stop();
                        StartWatch.Stop();
                        KeyCount = 0;
                    }
                    /* if (!Shock.Read() && !KeepWatch.IsRunning)
                     {
                         KeepWatch.Restart();
                     }
                     else if (Shock.Read() && KeepWatch.IsRunning)
                     {
                         if(KeepWatch.ElapsedMilliseconds < 1000&& Count > 0&& !mode)
                             Interlocked.Decrement(ref Count);
                         KeepWatch.Stop();
                     }
                     else if (KeepWatch.IsRunning && KeepWatch.ElapsedMilliseconds > 5000)
                     {
                         //Exit = true;
                         Thread.Sleep(100);
                         mode = mode ? false : true;
                        // ShutdownResetEvent.SetResult(0);
                     }
                     else if (KeepWatch.IsRunning && KeepWatch.ElapsedMilliseconds > 1000)
                     {
                         Count = 0;
                     }
                     else if (KeepWatch.IsRunning&&KeepWatch.ElapsedMilliseconds > 6000)
                     {
                         KeepWatch.Stop();
                     }*/

                    /*if (!Shock.Read() && !KeySign)
                    {
                        KeySign = true;
                        if(Count>0)
                        Interlocked.Decrement(ref Count);
                    }
                    if (Shock.Read() && KeySign)
                    {
                        KeySign = false;
                    }*/
                }
            });
            _ = Task.Factory.StartNew(() =>
            {
                var Tilt = Pi.Gpio[BcmPin.Gpio04];
                Tilt.PinMode = GpioPinDriveMode.Input;
                var KeySign = false;
                System.Diagnostics.Stopwatch KeepWatch = new System.Diagnostics.Stopwatch();

                while (true)
                {
                    Thread.Sleep(50);

                    /*  if (Tilt.Read() && !KeySign)
                       {
                           KeySign = true;
                           if (!KeepWatch.IsRunning)
                               Interlocked.Increment(ref Count);
                           KeepWatch.Restart();
                       }else if (!Tilt.Read() && KeySign)
                       {
                           KeySign = false;
                       }
                       if (KeepWatch.ElapsedMilliseconds > 4000) KeepWatch.Stop();*/

                    if (!Tilt.Read() && !KeySign)
                    {
                        KeySign = true;
                        if (!KeepWatch.IsRunning)
                            Interlocked.Increment(ref Count);
                        KeepWatch.Restart();
                    }
                    else if (Tilt.Read() && KeySign)
                    {
                        KeySign = false;
                    }
                    if (KeepWatch.ElapsedMilliseconds > 4000) KeepWatch.Stop();
                }
            });
            return await ShutdownResetEvent.Task.ConfigureAwait(false);
        }
    }
}