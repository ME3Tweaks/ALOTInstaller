// ALOTDetector.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <boost/filesystem.hpp>
#include <boost/filesystem/fstream.hpp>

namespace fs = boost::filesystem;

int main(int argc, char* argv[])
{
	if (argc != 3) {
		std::cout << "Usage: <game number (1,2,3)> <path to game root>" << "\n";
		std::cout << "Return codes:" << "\n";
		std::cout << "-1: Wrong number of arguments" << "\n";
		std::cout << "-2: Game root path not found" << "\n";
		std::cout << "-3: Invalid game number" << "\n";
		std::cout << "-4: Marker file not found" << "\n";
		std::cout << "0 : ALOT not found" << "\n";
		std::cout << "1 : ALOT found" << "\n";
		return -1;
	}

	int game = 0;
	try
	{
		game = std::stoi(argv[1]);
		if (game < 1 || game > 3) {
			std::cerr << "Invalid game number: " << game;
			return -4;
		}
	}
	catch (std::invalid_argument const& e)
	{
		std::cerr << "Bad input: std::invalid_argument thrown for argument 1" << '\n';
		return -3;
	}
	catch (std::out_of_range const& e)
	{
		std::cerr << "Integer overflow: std::out_of_range thrown for argument 1" << '\n';
		return -3;
	}

	std::string gameroot = argv[2];

	if (!fs::exists(gameroot)) {
		std::cerr << "Game path not found: " << gameroot << '\n';
		return -2;
	}

	std::string markerSubpath;

	switch (game) {
	case 1:
		markerSubpath = "BioGame/CookedPC/testVolumeLight_VFX.upk";
		break;
	case 2:
		markerSubpath = "BioGame/CookedPC/BIOC_Materials.pcc";
		break;
	case 3:
		markerSubpath = "BIOGame/CookedPCConsole/adv_combat_tutorial_xbox_D_Int.afc";
		break;
	}

	fs::path boostGameRoot(gameroot);
	fs::path full_path = boostGameRoot / markerSubpath;
	full_path = full_path.make_preferred();
	if (fs::exists(full_path)) {
		//Marker exists, open for reading
		std::ifstream stream(full_path.string(), std::ios::binary);
		stream.seekg(-4, std::ios_base::end);
		char buffer[4];
		stream.read(buffer, 4);

		//MEMI_TAG 0x494D454D
		if (buffer[0] == 0x4D &&
			buffer[1] == 0x45 &&
			buffer[2] == 0x4D &&
			buffer[3] == 0x49) {
			std::cout << "ALOT is installed for Mass Effect " << game << " instance at " << gameroot << '\n';
			return 1;
		}
		else {
			std::cout << "ALOT is not installed for Mass Effect " << game << " instance at " << gameroot << '\n';
			return 0;
		}
	}
	else {
		std::cerr << "Marker not found for Mass Effect " << game << " instance at " << gameroot << '\n';
		return -4;
	}
}

// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
