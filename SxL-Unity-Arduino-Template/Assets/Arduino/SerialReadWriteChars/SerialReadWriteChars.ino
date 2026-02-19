const int ledPin = 7;
const int buttonPin = 3;
const int joystickXPin = A2;
const int joystickYPin = A3;
char letter;

int charsSent = 0;
int sendTimer = 0;
int sendTime = 1;

bool isControlling = true;
bool isTransmitting = false;

bool buttonPressed = false;
int x;
int y;

void setup() {
  // initialize the serial communication:
  Serial.begin(9600);
  // initialize the ledPin as an output:
  pinMode(ledPin, OUTPUT);
  pinMode(joystickXPin, INPUT);
  pinMode(joystickYPin, INPUT);
  pinMode(buttonPin, INPUT);
}

void loop() {
  //Reading data from USB serial:
  // while(Serial.available()) {
  //   // read the most recent char or byte (which will be from 0 to 255):
  //   letter = Serial.read();

  //   // turn LED on or off based on message received, 'A' for on, 'B' for off:
  //   if(letter == 'n') {
  //     isControlling = true;
  //   }
  //   else if(letter == 'm') {
  //     isControlling = false;
  //   }
  // }

  if (digitalRead(buttonPin) == HIGH && buttonPressed == false) {
    if (isControlling) {
      Serial.print('j');
      isTransmitting = true;
    }
    buttonPressed = true;
  } else if (digitalRead(buttonPin) == LOW && buttonPressed == true) {
    buttonPressed = false;
  }

  //Sending data from USB serial:
  if (sendTimer > 0) {
    sendTimer -= 1;
  } else {
    sendTimer = sendTime;


    if (isTransmitting) {
      digitalWrite(ledPin, HIGH);
      isTransmitting = false;
    } else {
      digitalWrite(ledPin, LOW);
    }
  }

  x = analogRead(joystickXPin);
  y = analogRead(joystickYPin);

  if (x > 3800) {
    if (isControlling) {
      Serial.print('d');
      isTransmitting = true;
    }
  } else if (x < 1000) {
    if (isControlling) {
      Serial.print('a');
      isTransmitting = true;
    }
  } else {
  }

  if (y > 3800) {
    if (isControlling) {
      Serial.print('s');
      isTransmitting = true;
    }
  } else if (y < 1000) {
    if (isControlling) {
      Serial.print('w');
      isTransmitting = true;
    }
  } else {
  }

  //Serial.println(x);
  //Serial.println(y);

  //Quick delay:
  delay(100);
}
