# ChatGPT Refactor Notes

See UI changelog for frontend details.

## API changes
- Added `ChatController` with REST endpoints:
  - `GET api/Chat/Initial?skip=&take=`
  - `GET api/Chat/Total`
  - `GET api/Chat/{chatId}/Messages?skip=&take=`
  - `GET api/Chat/{chatId}/MessagesByDay?beforeUtc=`
- Added `CallController` with REST endpoints:
  - `GET api/Call?skip=&take=`
  - `GET api/Call/Total`
- Added paginated call methods to `ICallRepository`, `CallRepository`, `ICallService`, and `CallService`.
- Added `ChatNest.Tests` xUnit test project and solution entries.

## Verification note
The current execution environment does not include the .NET SDK, so API build/test execution could not be run here.
