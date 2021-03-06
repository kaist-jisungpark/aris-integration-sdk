#pragma once

#include <cstdint>

namespace Aris {
  namespace Common {

// These enumerations are essentially duplicates of what was generated
// by your protoc compiler; however, these serve the purpose of preventing
// ingress of those generated types into our code base.
enum class SonarFrequency : uint32_t { Low = 0, High = 1 };

enum class SystemType : uint32_t {
  Aris1800 = 0,
  Aris3000 = 1,
  Aris1200 = 2,
};

// ARIS broadcasts availability beacons to this port.
constexpr uint16_t kArisBeaconPort = 56124;

// ARIS accepts a TCP connection from a Controller on this port.
constexpr uint16_t kArisCommandPort = 56888;

// ARIS accepts platform header updates on this port.
constexpr uint16_t kArisPlatformHeaderUpdatePort = 700;

//-----------------------------------------------------------------------------

struct AcousticSettings {
  uint32_t        cookie;
  float           frameRate;
  uint32_t        pingMode;
  SonarFrequency  frequency;
  uint32_t        samplesPerBeam;
  uint32_t        sampleStartDelay;
  uint32_t        cyclePeriod;
  uint32_t        samplePeriod;
  uint32_t        pulseWidth;
  bool            enableTransmit;
  bool            enable150Volts;
  float           receiverGain;
};

}
}
