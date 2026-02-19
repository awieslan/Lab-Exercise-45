#include <WiFi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>

//Install Board Libraries for "esp32 by expressif".
//Make board type NodeMCU-32S
//Sample board supplier: https://www.amazon.com/HiLetgo-ESP-WROOM-32-Development-Microcontroller-Integrated/dp/B0718T232Z

// Replace with your network credentials
const char *ssid = "NETGEAR39";           //"TP-Link_24A4";
const char *password = "freshbreeze181";  //"91414380";

// Set up UDP
WiFiUDP udp;
const char *udpAddress = "172.168.10.2";  // Mac IP address
const int udpSenderPort = 5006;           // Port for sending data to Unity
const int udpReceiverPort = 5005;         // Port for receiving data from Unity
String sentMessage;
String receivedMessage;

//Char LED Read/Write Demo Variables
const int ledPin = 3;  // the pin that the LED is attached to

// I2C LCD: NodeMCU-32S uses SDA=21, SCL=22. Arduino Nano ESP32 uses SDA=5, SCL=6
#define LCD_SDA 21
#define LCD_SCL 22
#define LCD_I2C_ADDR 0x27  // Try 0x3F if display stays blank
LiquidCrystal_I2C lcd(LCD_I2C_ADDR, 16, 2);

bool blinking = false;   // Only true when "s" received - triggers 3 blinks
int blinks = 0;
int maxBlinks = 6;      // 6 state changes = 3 full blinks (on/off/on/off/on/off)

int score = 0;          // Number of times "s" received (game score)
int charsSent = 0;
int sendTimer = 0;
int sendTime = 3;  // Send every 3 iterations (with 100ms delay = ~300ms between sends)

void setup() {
  // initialize the serial communication:
  Serial.begin(9600);

  // Init UDP
  initUDP();
  sendUDP("Connected and Transmitting");

  // initialize the ledPin as an output:
  pinMode(ledPin, OUTPUT);

  // Initialize I2C and LCD (Wire must start before lcd.init)
  Wire.begin(LCD_SDA, LCD_SCL);
  delay(100);
  lcd.init();
  lcd.backlight();
  delay(100);
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("Score: 0");
  lcd.setCursor(0, 1);
  lcd.print("LCD Active");
}

void loop() {
  // Check WiFi connection status
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi disconnected! Attempting to reconnect...");
    //digitalWrite(ledPin, LOW);
    WiFi.begin(ssid, password);
    delay(5000);
    return;
  } 

  // Listen for a message from UDP (UDP -> Arduino ESP32)
  receivedMessage = receiveUDP();

  // Control the LED when "s" received (collectible collected in Unity)
  if (receivedMessage == "s") {
    score++;
    blinking = true;
    blinks = 0;  // Start fresh blink sequence
    digitalWrite(ledPin, HIGH);
    Serial.println("Score: " + String(score));
    lcd.setCursor(0, 0);
    lcd.print("Score: " + String(score) + "    ");  // Pad to clear old digits
  }

  //Sending data via UDP on a repeating timer (only if WiFi connected):
  if (sendTimer > 0) {
    sendTimer -= 1;
  } else {
    sendTimer = sendTime;

    if (blinks < maxBlinks && blinking) {
      if (blinks % 2 == 0) {
        digitalWrite(ledPin, HIGH);
      } else {
        digitalWrite(ledPin, LOW);
      }
      blinks++;

    } else {
      blinks = 0;
      blinking = false;
    }


    if(sentMessage != ""){
      // Send message to UDP
      sendUDP(sentMessage);
    }
  }

  //Quick delay:
  delay(100);
}

void initUDP() {
  // Connect to WiFi
  WiFi.begin(ssid, password);
  Serial.println("Connecting to WiFi...");
  Serial.print("SSID: ");
  Serial.println(ssid);

  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.print(".");
  }

  Serial.println("");
  Serial.println("Connected to WiFi!");
  Serial.print("SSID: ");
  Serial.println(WiFi.SSID());
  Serial.print("Arduino IP address: ");
  Serial.println(WiFi.localIP());
  Serial.print("Subnet Mask: ");
  Serial.println(WiFi.subnetMask());
  Serial.print("Gateway IP: ");
  Serial.println(WiFi.gatewayIP());
  Serial.print("DNS IP: ");
  Serial.println(WiFi.dnsIP());
  Serial.print("MAC Address: ");
  Serial.println(WiFi.macAddress());

  // Start listening for UDP on the receive port
  udp.begin(udpReceiverPort);
  Serial.println("UDP Initialized");
}

String receiveUDP() {
  int packetSize = udp.parsePacket();
  if (packetSize > 0) {
    char incomingPacket[255];
    int len = udp.read(incomingPacket, 255);
    if (len > 0) {
      incomingPacket[len] = 0;  //Null-terminate string for formatting.
      String message = String(incomingPacket);
      Serial.println("← Received: '" + message + "' from " + udp.remoteIP().toString() + ":" + String(udp.remotePort()) + " (" + String(len) + " bytes)");
      return message;
    } else {
      Serial.println("ERROR: Receive failed - read 0 bytes");
      return "";
    }
  }
  return "";
}

void sendUDP(String message) {
  int beginResult = udp.beginPacket(udpAddress, udpSenderPort);
  if (beginResult == 0) {
    Serial.println("ERROR: Send failed - beginPacket");
    delay(500);  // Back off on failure
    return;
  }

  size_t written = udp.write((const uint8_t *)message.c_str(), message.length());
  int endResult = udp.endPacket();

  if (endResult == 1) {
    Serial.println("→ Sent: '" + message + "' to " + String(udpAddress) + ":" + String(udpSenderPort) + " (" + String(written) + " bytes)");
    delay(250);  // Give ESP32 WiFi stack time to clear TX buffer before next send
  } else {
    Serial.println("ERROR: Send failed - endPacket (result: " + String(endResult) + ")");
    delay(500);  // Longer back-off when buffer is full - lets WiFi stack recover
  }
}
