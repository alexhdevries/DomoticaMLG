#include <iostream>
#include <memory>
#include <Windows.h>

using namespace std;

int main() {
	int intArray[4] = { 3683, 123895, 4368, 3458 };

	int* pointer = intArray;

	int k = 1024 * 1024; // 4194304
	while (true) {
		shared_ptr<int> variableSizeArray(new int[k], default_delete<int[]>());

		Sleep(500);
	}
getchar();

	return 0;
}