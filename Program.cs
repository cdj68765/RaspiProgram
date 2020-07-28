using System;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace ConsoleApp1
{
    internal class tm1637
    {
        /*  private GpioPin clkPin;
          private GpioPin dataPin;*/
        private IGpioPin clkPin;
        private IGpioPin dataPin;
        private byte brightness = 0;
        private byte[] digits = { 0x00, 0x00, 0x00, 0x00 };

        public tm1637()
        {
            //Pi.Init<BootstrapWiringPi>();
            /* clkPin = gpio.OpenPin(pinClock);
             clkPin.SetDriveMode(GpioPinDriveMode.Output);

             dataPin = gpio.OpenPin(pinData);
             dataPin.SetDriveMode(GpioPinDriveMode.Output);*/

            Pi.Init<BootstrapWiringPi>();
            clkPin = Pi.Gpio[BcmPin.Gpio23];
            dataPin = Pi.Gpio[BcmPin.Gpio24];
            // Configure the pin as an output
            clkPin.PinMode = GpioPinDriveMode.Output;
            dataPin.PinMode = GpioPinDriveMode.Output;

            clkPin.Write(GpioPinValue.Low);
            dataPin.Write(GpioPinValue.Low);
            //Init Display
            for (int i = 0; i < 4; i++)
            {
                this.digits[i] = 0x00;
            }
            //Set Brightness
            setBrightness(brightness);
            //Display blinks during Startup
            startupShow();
        }

        ~tm1637()
        {
            write("    ");
        }

        private async void startupShow()
        {
            for (int i = 0; i < 10; i++)
            {
                write("----");
                await Task.Delay(200);
                write("    ");
                await Task.Delay(200);
            }
        }

        public void setBrightness(int bright)
        {
            this.brightness = (byte)(bright & 0x07);
            update();
        }

        private void update()
        {
            //Start transmission
            startDisp();//开始写入指令 C高D高D低
            writeByte(0x40);//40H模式地址自动+1,44H模式固定地址
            stopDisp();//结束写入指令 C低D低C高D高
            //上述完成数据指令设置

            //设置光标位置为11000000，光标位置每次输入+1
            startDisp();
            writeByte(0xC0);//设置首地址
            //Write text
            for (int i = 0; i < digits.Length; i++)
            {
                writeByte(digits[i]);
            }
            stopDisp();

            startDisp();
            writeByte((byte)(0x88 | this.brightness));
            stopDisp();
        }

        public void write(string w)
        {
            //Convert from String to Byte
            char c;
            for (int i = 0; i < digits.Length; i++)
            {
                if (w.Length - 1 >= i)
                    c = w[i];
                else
                    c = ' ';
                this.digits[i] = encode(c);
            }
            update();
        }

        private void startDisp()
        {
            clkPin.Write(GpioPinValue.High);
            dataPin.Write(GpioPinValue.High);
            dataPin.Write(GpioPinValue.Low);
        }

        private void stopDisp()
        {
            clkPin.Write(GpioPinValue.Low);
            dataPin.Write(GpioPinValue.Low);
            clkPin.Write(GpioPinValue.High);
            dataPin.Write(GpioPinValue.High);
        }

        private void writeByte(byte input)
        {
            //Bit Banging magic is here
            for (int i = 0; i < 8; i++)
            {
                clkPin.Write(GpioPinValue.Low);//输入前拉低时钟

                if ((input & 0x01) == 1)//判断每一位的高低
                {
                    dataPin.Write(GpioPinValue.High);
                }
                else
                {
                    dataPin.Write(GpioPinValue.Low);
                }
                input >>= 1;//每写入完毕一次，移动一位

                clkPin.Write(GpioPinValue.High);//输入完毕拉高时钟
            }

            //Suppress Answere写入完毕以后，等待IC应答
            clkPin.Write(GpioPinValue.Low);//先拉低C
            clkPin.Write(GpioPinValue.High);//需要判断D是否为0此期间C一直拉高
            clkPin.Write(GpioPinValue.Low);//应答完毕以后拉低C
        }

        private static readonly byte[] brackets = { 0x39, 0x0f };

        private static readonly byte[] numbers = {0x3f, 0x06, 0x5b, 0x4f, 0x66,
                            0x6d, 0x7d, 0x07, 0x7f, 0x6f};

        private static readonly byte[] characters = {0x77, 0x7c, 0x39, 0x5e, 0x79,
                            0x71, 0x6f, 0x76, 0x30, 0x1e,
                            0x00, 0x38, 0x00, 0x00, 0x5c,
                            0x73, 0x67, 0x50, 0x5b, 0x78,
                            0x3e, 0x1c, 0x00, 0x00, 0x6e,
                            0x5b};

        private static readonly byte[] characters2 = { 0xbf, 0x86, 0xdb, 0xcf, 0xe6, 0xed, 0xfd, 0x87, 0xff, 0xef };

        private byte encode(char ch)
        {
            if (ch >= '0' && ch <= '9')
                return numbers[(int)ch - 48];
            else if (ch >= 'a' && ch <= 'z')
                return characters[(int)ch - 97];
            else if (ch >= 'A' && ch <= 'Z')
                return characters[(int)ch - 65];
            else if (ch == '[')
                return brackets[0];
            else if (ch == ']')
                return brackets[1];
            else if (ch == '(' || ch == ')')
                return brackets[(int)ch - 40];
            else if (ch == '-')
                return 0x40;
            else if (ch == '_')
                return 0x08;
            else if (ch == '}')
                return 0x70;
            else if (ch == '{')
                return 0x46;
            return 0x00;
        }
    }

    internal class Program
    {
        private static byte[] Characters = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f, 0x77, 0x7c, 0x39, 0x5e, 0x79, 0x71 };//0~9,A,b,C,d,E,F

        private static readonly byte[] numbers = {0x3f, 0x06, 0x5b, 0x4f, 0x66,
                            0x6d, 0x7d, 0x07, 0x7f, 0x6f};

        private static readonly byte[] characters2 = { 0xbf, 0x86, 0xdb, 0xcf, 0xe6, 0xed, 0xfd, 0x87, 0xff, 0xef };

        private static void Main(string[] args)
        {
            Pi.Init<BootstrapWiringPi>();//初始化通信接口，分配内存空间等
            var clkPin = Pi.Gpio[BcmPin.Gpio23];//引用16接口
            var dataPin = Pi.Gpio[BcmPin.Gpio24];//引用18接口
            clkPin.PinMode = GpioPinDriveMode.Output;//设置16接口模式为输出
            dataPin.PinMode = GpioPinDriveMode.Output;//设置18接口模式为输出
            clkPin.Write(GpioPinValue.High);//初始化电平为低,可不加
            dataPin.Write(GpioPinValue.High);//初始化电平为低,可不加
            Console.CancelKeyPress += delegate
            {
                startDisp();
                writeByte(0x80);
                stopDisp(); 
            };
            // var B = Pi.Gpio[BcmPin.Gpio25];//引用16接口
            //var G = Pi.Gpio[BcmPin.Gpio08];//引用18接口
            //var R = Pi.Gpio[BcmPin.Gpio07];//引用18接口
            /*   B.PinMode = GpioPinDriveMode.Output;
               G.PinMode = GpioPinDriveMode.Output;
               R.PinMode = GpioPinDriveMode.Output;
               while (true)
               {
                   B.Write(GpioPinValue.High);
                   G.Write(GpioPinValue.High);
                   R.Write(GpioPinValue.High);
                   Thread.Sleep(1000);
                   B.Write(GpioPinValue.Low);
                   G.Write(GpioPinValue.High);
                   R.Write(GpioPinValue.High);
                   Thread.Sleep(1000);
                   B.Write(GpioPinValue.High);
                   G.Write(GpioPinValue.Low);
                   R.Write(GpioPinValue.High);
                   Thread.Sleep(1000);
                   B.Write(GpioPinValue.High);
                   G.Write(GpioPinValue.High);
                   R.Write(GpioPinValue.Low);
                   Thread.Sleep(1000);
                   B.Write(GpioPinValue.High);
                   G.Write(GpioPinValue.Low);
                   R.Write(GpioPinValue.Low);
                   Thread.Sleep(1000);
               }*/

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
            void Show2()
            {
                //设置基本参数
                startDisp();//开始写入指令
                writeByte(0x40);//指定功能参数
                stopDisp();//结束写入指令

                //设置显示地址以及显示内容
                startDisp();
                writeByte(0xC0);//设置首地址，指向第一个字符
                var Date = DateTime.Now.ToString("hhmm").ToCharArray();//获得当前日期，并表示为小时分钟
                byte[] Characters = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f, 0x77, 0x7c, 0x39, 0x5e, 0x79, 0x71 };//0~9,A,b,C,d,E,F
                /*for (int i = 0; i < Date.Length; i++)
                {
                    if (i != 1) writeByte(Characters[Date[i] - 48]);//从Characters数组根据索引获得字符显示的编码
                    else writeByte((byte)(Characters[Date[1] - 48] + 0x80));//第二个字符带有冒号，因此将第一位空置拉高
                }*/
                startDisp();
                writeByte(0xC0);
                writeByte(Characters[Date[0] - 48]);
                stopDisp();

                startDisp();
                writeByte(0xC1);
                writeByte(Characters[Date[1] - 48]);
                stopDisp();

                startDisp();
                writeByte(0xC2);
                writeByte(Characters[Date[2] - 48]);
                stopDisp();

                startDisp();
                writeByte(0xC3);
                writeByte(Characters[Date[3] - 48]);
                stopDisp();

                //开始写入亮度
                startDisp();
                writeByte(0x8f);
                stopDisp();
            }
            /*  for (byte i = 0x40; i < 0x7f; i++)
              {
                  Show((byte)(i | 0x80));
                  Thread.Sleep(10000);
              }*/
           /* while (true)
            {
                Show2();
            }*/
            while (true)
            {
                var Date = DateTime.Now.ToString("hhmm").ToCharArray();
                Show(0xC0, numbers[Date[0] - 48]);
                Show(0xC1, (byte)(numbers[Date[1] - 48] + 0x80));
                Show(0xC2, numbers[Date[2] - 48]);
                Show(0xC3, numbers[Date[3] - 48]);
                Thread.Sleep(1000);
                Date = DateTime.Now.ToString("mmss").ToCharArray();
                Show(0xC0, numbers[Date[0] - 48]);
                Show(0xC1, (byte)(numbers[Date[1] - 48] + 0x80));
                Show(0xC2, numbers[Date[2] - 48]);
                Show(0xC3, numbers[Date[3] - 48]);
                Thread.Sleep(1000);
            }
            foreach (var item in numbers)
            {
                /* if (item == 0x7f) Show(0x00 + 0x80);
                 else Show((byte)(item + 0x80));*/
                Show(0xC0, (byte)(item));
                Show(0xC1, (byte)(item + 0x80));
                Show(0xC2, (byte)(item));
                Show(0xC3, (byte)(item));
                Thread.Sleep(1000);
            }
            Console.WriteLine();
            /*if (ij == 0xC6)
            {
                ij = 0xC0;
            }*/

            startDisp();
            writeByte(0x80);
            stopDisp();

            Thread.Sleep(100);
        }
    }
}