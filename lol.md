## Задание 5.1: Таймер на LCD 16x2 (мм:сс)

Подключите LCD 16x2 по I2C (SDA к A4, SCL к A5, VCC/GND). Установите библиотеку LiquidCrystal_I2C. Используйте millis() для подсчёта времени без задержек.[1][2]

```cpp
#include <Wire.h>
#include <LiquidCrystal_I2C.h>
LiquidCrystal_I2C lcd(0x27, 16, 2);

unsigned long startTime = 0;

void setup() {
  lcd.init();
  lcd.backlight();
  startTime = millis();
}

void loop() {
  unsigned long elapsed = (millis() - startTime) / 1000;
  int minutes = elapsed / 60;
  int seconds = elapsed % 60;
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("Время: ");
  if (minutes < 10) lcd.print("0");
  lcd.print(minutes);
  lcd.print(":");
  if (seconds < 10) lcd.print("0");
  lcd.print(seconds);
  delay(1000);
}
```

## Задание 5.2: Бегущая строка с сердцем на LCD 16x2

Создайте символ сердца в массиве byte (5x8 пикселей). Используйте lcd.createChar() и lcd.scrollDisplayLeft() для прокрутки справа налево.[3][4]

```cpp
#include <Wire.h>
#include <LiquidCrystal_I2C.h>
LiquidCrystal_I2C lcd(0x27, 16, 2);

byte heart[8] = {
  B00000, B01010, B11111, B11111, B01110, B00100, B00000, B00000
};

void setup() {
  lcd.init();
  lcd.backlight();
  lcd.createChar(0, heart);
}

void loop() {
  lcd.clear();
  lcd.setCursor(16, 0);
  lcd.print(" Hello World ");
  lcd.write(byte(0));
  lcd.scrollDisplayLeft();
  delay(300);
}
```

## Задание 5.3: Изображение 64x64 на OLED 128x64

Используйте библиотеку Adafruit_SSD1306 и Adafruit_GFX. Сгенерируйте bitmap на https://pkolt.github.io/bitmap_editor (монохром 64x64). Разместите в правой половине (x=64).[5]

```cpp
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
Adafruit_SSD1306 display(128, 64, &Wire, -1);

// Пример bitmap (замените на свой)
const unsigned char myBitmap [] PROGMEM = {
  // 64x64 байты данных здесь (512 байт)
  0x00, 0x00, /* ... генерируйте на сайте ... */
};

void setup() {
  display.begin(SSD1306_SWITCHCAPVCC, 0x3C);
  display.clearDisplay();
  display.drawBitmap(64, 0, myBitmap, 64, 64, WHITE);
  display.display();
}
```

## Задание 5.4: Меню с кнопками на OLED

Добавьте 2 кнопки (к пинам 2,3 с pullup). Отобразите 3 пункта: "Еда", "Энергия", "Здоровье". Рамка вокруг активного (используйте drawRect). Первая кнопка переключает.[6][7]

```cpp
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
Adafruit_SSD1306 display(128, 64, &Wire, -1);

int menuItem = 0;
int btn1 = 2, btn2 = 3;

String menu[] = {"Еда", "Энергия", "Здоровье"};

void setup() {
  pinMode(btn1, INPUT_PULLUP);
  pinMode(btn2, INPUT_PULLUP);
  display.begin(SSD1306_SWITCHCAPVCC, 0x3C);
}

void loop() {
  display.clearDisplay();
  for (int i = 0; i < 3; i++) {
    int y = i * 20;
    display.setCursor(0, y);
    display.print(menu[i]);
    if (i == menuItem) {
      display.drawRect(0, y - 2, 60, 18, WHITE);  // Рамка
    }
  }
  display.display();

  if (!digitalRead(btn1)) menuItem = (menuItem + 1) % 3;
  delay(200);
}
```

## Задание 5.5: Симуляция характеристик

Добавьте значения 1-100. Еда/энергия уменьшаются со временем; здоровье растёт >80, падает <20. Вторая кнопка увеличивает выбранную. Game Over при здоровье=0.[8][9]

```cpp
// Продолжение 5.4, добавьте:
int food = 100, energy = 100, health = 100;
int values[3] = {100, 100, 100};  // еда, энергия, здоровье
unsigned long lastUpdate = 0;

void loop() {
  // ... отрисовка меню с значениями: display.print(values[i]);

  if (millis() - lastUpdate > 5000) {  // Каждые 5с
    values[0]--;  // Еда
    values[1]--;  // Энергия
    if (values[0] > 80 && values[1] > 80) values[2]++;
    else if (values[0] < 20 || values[1] < 20) {
      values[2] -= (values[0] < 20 && values[1] < 20) ? 2 : 1;
    }
    if (values[2] <= 0) {
      display.clearDisplay();
      display.setCursor(20, 30);
      display.print("Game Over");
      display.display();
      while(1);
    }
    lastUpdate = millis();
  }

  if (!digitalRead(btn1)) menuItem = (menuItem + 1) % 3;
  if (!digitalRead(btn2)) values[menuItem] = min(100, values[menuItem] + 10);
  delay(200);
}
```

Схемы: LCD/OLED по I2C стандартно. Кнопки: 10кОм pullup к 5V. Тестируйте в Arduino IDE.[4][5]

Источники
[1] Creating a Count up timer that will display HH:MM:SS on ... https://forum.arduino.cc/t/creating-a-count-up-timer-that-will-display-hhss-on-an-lcd-display/1148042
[2] clock with millis - Programming https://forum.arduino.cc/t/clock-with-millis/648199
[3] LCD Scroll Text - Programming - Arduino Forum https://forum.arduino.cc/t/lcd-scroll-text/210755
[4] Create Custom Characters for the I2C LCD Easily https://arduinointro.com/articles/projects/create-custom-characters-for-the-i2c-lcd-easily
[5] GitHub - JyothiPal/Displaying-a-Custom-128x64-Bitmap-on-an-SSD1306-OLED-with-Arduino: This Arduino project demonstrates how to load and display a custom 128x64 monochrome bitmap image on an SSD1306 OLED screen using Adafruit libraries. The image is stored in PROGMEM and rendered with drawBitmap(), making it great for adding visual elements like logos to embedded devices. https://github.com/JyothiPal/Displaying-a-Custom-128x64-Bitmap-on-an-SSD1306-OLED-with-Arduino
[6] Arduino OLED Display Menu With Option to Select - Instructables https://www.instructables.com/Arduino-OLED-Display-Menu-With-Option-to-Select/
[7] Arduino OLED Menu Tutorial (for beginners - YouTube https://www.youtube.com/watch?v=HVHVkKt-ldc
[8] Menu Navigation: Arduino OLED Display System || U8glib Library (1.3 inch OLED display) https://www.youtube.com/watch?v=7zu6XoGP1KI
[9] Zombie Snack - Arduino/LCD game https://forum.arduino.cc/t/zombie-snack-arduino-lcd-game/8793
[10] BasicOLEDMenu - Wokwi ESP32, STM32, Arduino Simulator https://wokwi.com/projects/323967017900048980
[11] how to display HH:MM:SS on LCD - Arduino Forum https://forum.arduino.cc/t/how-to-display-hh-mm-ss-on-lcd/279405
[12] How to Display Custom Characters - Arduino LCD Display - YouTube https://www.youtube.com/watch?v=r0NVDFI-134
[13] GitHub - styropyr0/oled.h: OLED Display Library for SSD1306 OLED displays with Advanced Functionalities for Arduino/ESP, such as 15 different Progress Bars and External Font support, Drawing bitmaps, as well as other intelligent features. https://github.com/styropyr0/oled.h
[14] Finally Finished My Button Box Project V2 with Screens and Profile Selector https://www.reddit.com/r/arduino/comments/1fbor6a/finally_finished_my_button_box_project_v2_with/
[15] How to Display "Hello World" Using 16x2 LCD with I2C Module and Arduino UNO Board https://www.youtube.com/watch?v=HrZiJZow7K0
[16] How To use special character on LCD | Arduino FAQs https://arduinogetstarted.com/faq/how-to-use-special-character-on-lcd
[17] Arduino: Display OLED 0.96" 128x64 SSD1306 I2C https://www.youtube.com/watch?v=GT7LV30WWgw
