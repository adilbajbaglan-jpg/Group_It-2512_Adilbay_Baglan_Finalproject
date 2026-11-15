#include "libraryfees.h"

int32_t library_fee(int32_t late_days)
{
    if (late_days < 0) return -1;
    if (late_days >= 30) return 3000;
    return late_days * 100;
}
