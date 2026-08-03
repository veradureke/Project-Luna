using UnityEngine;

public class NASAKnowledgeBase : MonoBehaviour
{
    // NASA knowledge base — add facts about each object
    public static string GetContext(string objectName)
    {
        objectName = objectName.ToLower();

        if (objectName.Contains("space launch system") ||
            objectName.Contains("sls"))
        {
            return @"NASA Space Launch System (SLS) Facts:
- SLS is the most powerful rocket NASA has ever built
- Generates 8.8 million pounds of thrust at launch
- Stands 322 feet tall with Orion and the Launch Abort System
- Uses 4 RS-25 engines and 2 solid rocket boosters
- Block 1 configuration can send 27 metric tons to the Moon
- First launched November 16, 2022 for Artemis I mission
- Designed to send astronauts to the Moon and eventually Mars
- Core stage contains liquid hydrogen and liquid oxygen propellants";
        }

        if (objectName.Contains("orion"))
        {
            return @"NASA Orion Spacecraft Facts:
- Orion is NASA's spacecraft designed to carry astronauts beyond Earth orbit
- Can carry up to 4 crew members
- Designed for missions to the Moon, asteroids, and eventually Mars
- Features a Launch Abort System for crew safety during emergencies
- European Service Module provides power, propulsion and life support
- Heat shield is 16.5 feet in diameter — largest ever built
- Can withstand reentry temperatures of 5000 degrees Fahrenheit
- First crewed flight planned for Artemis II mission around the Moon";
        }

        if (objectName.Contains("rs-25") ||
            objectName.Contains("engine"))
        {
            return @"RS-25 Engine Facts:
- RS-25 engines powered the Space Shuttle for 30 years
- Each engine produces 512,000 pounds of thrust at sea level
- Burns liquid hydrogen and liquid oxygen at -423°F and -297°F
- SLS uses 4 RS-25 engines on its core stage
- Operates at 109% of original rated power for SLS missions
- One of the most reliable rocket engines ever built
- Each engine has over 1 million parts
- Aerojet Rocketdyne manufactures the engines";
        }

        if (objectName.Contains("apollo") ||
            objectName.Contains("lunar module"))
        {
            return @"Apollo Lunar Module Facts:
- The Apollo Lunar Module was the first crewed vehicle to land on the Moon
- Consisted of a descent stage and an ascent stage
- Stood 23 feet tall and weighed 33,500 pounds fully fueled
- Descent engine throttled from 1,050 to 10,500 pounds of thrust
- 12 astronauts walked on the Moon using the Lunar Module
- First lunar landing was Apollo 11 on July 20, 1969
- Astronauts Neil Armstrong and Buzz Aldrin landed at the Sea of Tranquility
- The ascent stage used a single engine to return crew to the Command Module";
        }

        if (objectName.Contains("z2") ||
            objectName.Contains("spacesuit") ||
            objectName.Contains("suit"))
        {
            return @"NASA Z-2 Spacesuit Facts:
- The Z-2 is NASA's prototype for the next generation of spacesuits
- Designed for planetary surface exploration including the Moon and Mars
- Features a hard upper torso for durability in harsh environments
- Uses a rear-entry design for easier donning and doffing
- Incorporates advanced life support systems
- Electroluminescent wire technology for visibility in dark environments
- Designed to work with planetary rovers and habitat airlocks
- Underwent testing at NASA's Neutral Buoyancy Laboratory";
        }

        if (objectName.Contains("moon") ||
            objectName.Contains("lunar"))
        {
            return @"Moon Facts:
- The Moon is Earth's only natural satellite
- Located 238,855 miles from Earth on average
- Surface temperature ranges from -280°F to 260°F
- Has no atmosphere or global magnetic field
- Lunar regolith (soil) is made of fine crushed rock and glass
- Water ice exists in permanently shadowed craters at the poles
- NASA's Artemis program aims to establish a sustainable presence on the Moon
- The lunar south pole is the target for Artemis missions due to water ice";
        }

        // Default context for unknown objects
        return @"NASA Artemis Program Facts:
- Artemis is NASA's program to return humans to the Moon
- Named after the twin sister of Apollo in Greek mythology
- Goal is to land the first woman and first person of color on the Moon
- Artemis I launched in November 2022 as an uncrewed test flight
- Artemis II will be the first crewed flight around the Moon
- Artemis III will land astronauts at the lunar south pole
- Gateway lunar space station will support long-term lunar exploration
- Commercial partners include SpaceX, Blue Origin, and others";
    }
}