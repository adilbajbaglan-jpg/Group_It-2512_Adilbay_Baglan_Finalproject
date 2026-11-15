#ifndef LIBRARYFEES_H
#define LIBRARYFEES_H
#ifdef _WIN32
#define LIBRARY_API __declspec(dllexport)
#else
#define LIBRARY_API __attribute__((visibility("default")))
#endif
#include <stdint.h>
LIBRARY_API int32_t library_fee(int32_t late_days);
#endif
