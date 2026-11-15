# Как показать LibraryDesk на защите

## Демо за две минуты

1. Открой Start.cmd, введи `demo`. Демо каждый раз берёт новую временную копию данных.
2. Найди первую выдачу B03. Скажи: “The book is available. The standard policy gives the reader fourteen days.” Дата 15 ноября превращается в 29 ноября.
3. Покажи повторную выдачу. “The book is already on loan. The operation fails and the file stays unchanged.” Найди причину в Shelf.CheckAvailability.
4. Продли и верни книгу. Срок после продления — 6 декабря. Возврат 9 декабря опоздал на три дня. “The C library calculates a fee of three hundred tenge.”
5. Запусти Tests.cmd. Покажи тест 11 для полного сценария и тест 21 для сохранности файла после ошибки. Объясни конкретную проверку в том коде, который сам разбирал.
6. Для живого ввода введи `books`, затем `issue B04 Kai 2026-11-15 extended`. Ожидаемый срок — 13 декабря. Повторная команда должна показать отказ. Рабочий файл сохраняет изменения при перезапуске.

## Какие файлы открыть

| Вопрос преподавателя | Короткий ответ на английском | Что открыть |
| --- | --- | --- |
| What is a pure function? | The same inputs give the same result. It does not read a file or change external state. | Policies.Period in Lending.cs |
| Why use Bind here? | The next step can fail. Bind passes a successful value forward and skips the next step after an error. | Shelf.Issue and Result.Bind |
| How is Map different? | Map changes the successful value. Here it adds the new loan to a new shelf. | Shelf.Issue, final Map(AddLoan) |
| What is Maybe for? | A book search can find nothing. OrFail turns that missing book into a domain error. | FindBook, FindMaybe, OrFail |
| How do you prevent an invalid return? | The loan must be active. The date must follow or equal the last action. The fee is calculated after both checks. | Loan.Return |
| Where is currying? | Period takes the number of days and returns a function that takes the start date. | Policies.Standard and Extended |
| When does yield run? | It runs as we enumerate the sequence. ToList collects a reusable snapshot. | Reports.LateLoans and test 15 |
| Which code calls C? | The adapter calls library_fee through P/Invoke. It returns the actual fee used in the receipt. | NativeFees.Quote and native/libraryfees.c |
| How does a report break ties? | Books with the same count are sorted by book ID. | Reports.Popular and test 14 |
| What did you implement? | Name a change and a test you actually wrote. Explain the input, rule and result. | Your own file, method and test |

## Пример изменения прямо на защите

Упражнение с тестом. Добавь в тест 16 строку `Equal(3000, Ok(NativeFees.Quote(31)));`. До запуска предскажи ответ. 30 и 31 день дают одинаковые 3000 из-за верхней границы. Запусти Tests.cmd. Чтобы проверить, что тест способен заметить ошибку, временно укажи 3100. Убедись, что получишь FAIL, верни правильное ожидание и повтори запуск.

Упражнение с логикой. На отдельной рабочей копии поменяй продление с 7 на 10 дней в Policies.Extension. Сначала запусти тесты со старыми ожиданиями. Найди, почему тестам 08 и 11 нужны новые даты или суммы. В тесте 11 возврат через 10 дней после исходного срока теперь попадает ровно на новый срок, поэтому штраф должен стать 0. Проверь новые ожидания и повтори demo. Объясни результат своими словами.

## Порядок подготовки

Сначала пройди один успешный и один неуспешный сценарий. Затем проследи в коде путь Program → Storage.Update → Ledger.Append → Actions.Apply → Shelf.Issue. После этого проверь, почему функция из Bind не вызывается в тесте 07. Личный отчёт заполняй по своим реальным изменениям и тестам.
