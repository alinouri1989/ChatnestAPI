# ChatNest API and SignalR Guide for Native Android/Kotlin

This document is generated from the current `chatnest.api` source code. It is intended for native Android/Kotlin development against the ASP.NET Core backend.

## Base URLs

Local launch settings:

```text
HTTP:  http://localhost:5069
HTTPS: https://localhost:7042
Swagger: /swagger
```

For Android emulator, replace `localhost` with `10.0.2.2`:

```text
http://10.0.2.2:5069
https://10.0.2.2:7042
```

Production should use the deployed HTTPS API origin.

## Current Client Architecture

The current service/UI contract is REST-first for data loading and message mutations:

- Use REST for initial chat pages, message history, call logs, text messages, file messages, attachment forwarding, and message deletion.
- Keep SignalR connected for realtime events: new message broadcasts, ack/read delivery state, typing, presence, notifications, group/profile updates, and call signaling.
- Do not use SignalR as the primary source for chat/message history in new Android code. Some legacy hub read methods still exist for compatibility, but the web UI has moved active loading to REST.

## Authentication

Most REST endpoints and all SignalR hubs require JWT bearer authentication.

REST header:

```http
Authorization: Bearer <jwt>
Content-Type: application/json
```

SignalR authentication:

```kotlin
HubConnectionBuilder.create("$baseUrl/hub/Chat")
    .withAccessTokenProvider(Single.defer { Single.just(tokenStore.accessToken) })
    .build()
```

The backend also accepts the SignalR token as the `access_token` query parameter for `/hub/Chat`, `/hub/Call`, and `/hub/Notification`. The official SignalR Java client supports Android apps, bearer-token factories, JSON protocol only, and WebSockets only. See Microsoft Learn: https://learn.microsoft.com/en-us/aspnet/core/signalr/java-client

## Android Dependencies

Use current versions in your Android project. Microsoft documents the Java/Android SignalR dependency as `com.microsoft.signalr:signalr:<version>` and points to Maven Central for the latest version.

```kotlin
dependencies {
    implementation("com.squareup.retrofit2:retrofit:2.11.0")
    implementation("com.squareup.retrofit2:converter-moshi:2.11.0")
    implementation("com.squareup.okhttp3:logging-interceptor:4.12.0")
    implementation("com.microsoft.signalr:signalr:<latest-stable>")
    implementation("io.reactivex.rxjava3:rxjava:3.1.10")
    implementation(platform("com.google.firebase:firebase-bom:<latest-stable>"))
    implementation("com.google.firebase:firebase-messaging")

    // Needed if you use backend LiveKit token flow for audio/video media.
    implementation("io.livekit:livekit-android:<current-version>")
}
```

LiveKit Android installation and permissions are documented by LiveKit at https://docs.livekit.io/transport/sdk-platforms/android/. LiveKit requires microphone/camera runtime permissions for voice/video.

## Firebase Push Notifications

Add your Android app to the Firebase project, download `google-services.json`, place it in the Android app module, and apply the Google Services Gradle plugin. The app must send its FCM registration token to ChatNest after login and whenever Firebase refreshes it.

REST endpoints:

```http
POST /api/User/FirebaseToken
DELETE /api/User/FirebaseToken
Authorization: Bearer <jwt>
Content-Type: application/json
```

Body:

```json
{
  "token": "<firebase-fcm-registration-token>",
  "platform": "android"
}
```

Kotlin token registration:

```kotlin
data class FirebaseTokenRequest(
    val token: String,
    val platform: String = "android"
)

interface UserApi {
    @POST("api/User/FirebaseToken")
    suspend fun registerFirebaseToken(@Body body: FirebaseTokenRequest)

    @HTTP(method = "DELETE", path = "api/User/FirebaseToken", hasBody = true)
    suspend fun removeFirebaseToken(@Body body: FirebaseTokenRequest)
}

fun syncFirebaseToken(userApi: UserApi) {
    FirebaseMessaging.getInstance().token
        .addOnSuccessListener { token ->
            lifecycleScope.launch {
                userApi.registerFirebaseToken(FirebaseTokenRequest(token))
            }
        }
}
```

Token refresh service:

```kotlin
class ChatNestFirebaseMessagingService : FirebaseMessagingService() {
    override fun onNewToken(token: String) {
        super.onNewToken(token)
        // If the user is logged in, call POST /api/User/FirebaseToken with this token.
        // Keep this work in a repository/worker that can attach the current JWT.
    }

    override fun onMessageReceived(message: RemoteMessage) {
        super.onMessageReceived(message)
        val chatId = message.data["chatId"]
        val chatType = message.data["chatType"]
        // Show/update a local notification if the app is foregrounded.
    }
}
```

Android manifest:

```xml
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />

<application>
    <service
        android:name=".ChatNestFirebaseMessagingService"
        android:exported="false">
        <intent-filter>
            <action android:name="com.google.firebase.MESSAGING_EVENT" />
        </intent-filter>
    </service>
</application>
```

For Android 13+, request `POST_NOTIFICATIONS` at runtime. The server sends chat message pushes with Android high priority and notification channel id `chat_messages`, so create that notification channel in the Android app before showing notifications.

## JSON Naming

ASP.NET Core uses its default JSON naming policy, so C# properties are serialized in camelCase.

Example:

```json
{
  "displayName": "Ali Nouri",
  "profilePhoto": "https://..."
}
```

Dates are ISO-8601 strings. Send UTC where a hub method accepts dates, especially call timing and message cursors.

## Enums

The backend accepts enum values as numbers by default.

```text
Theme: DefaultSystemMode=0, Light=1, Dark=2
MessageContent: Text=0, Image=1, Video=2, Audio=3, File=4, Location=5
GroupParticipant: Admin=0, Member=1, Former=2
GroupKind: Group=0, Channel=1
CallType: Voice=0, Video=1
CallStatus: Pending=0, Accepted=1, Declined=2, Canceled=3, Missed=4, Ongoing=5
```

## Common Error Shapes

REST validation errors may be ASP.NET `ModelState` dictionaries. Application errors usually look like:

```json
{ "message": "خطا یا پیام اعتبارسنجی" }
```

Server errors may include:

```json
{
  "message": "خطای غیرمنتظره‌ای رخ داده است!",
  "errorDetails": "..."
}
```

SignalR validation and server errors are emitted as client events:

```text
ValidationError({ message })
UnexpectedError({ message, errorDetails })
ConnectionError({ message, errorDetails })
```

## REST API

Routes use `[Route("api/[controller]/[action]")]` unless noted otherwise.

### Auth

`POST /api/Auth/SignUp`

Anonymous. Registers a new email/password account.

```json
{
  "displayName": "Ali Nouri",
  "email": "ali@example.com",
  "password": "Password1",
  "passwordAgain": "Password1",
  "birthDate": "1990-01-01T00:00:00Z",
  "phoneNumber": "09123456789"
}
```

Success:

```json
{ "message": "کاربر با موفقیت ثبت شد." }
```

`POST /api/Auth/SignInEmail`

Anonymous.

```json
{
  "email": "ali@example.com",
  "password": "Password1"
}
```

Success:

```json
{ "token": "<jwt>" }
```

`POST /api/Auth/SignInGoogle`

Anonymous. Body is a Firebase provider user object:

```json
{
  "uid": "firebase-uid",
  "email": "ali@example.com",
  "emailVerified": true,
  "displayName": "Ali Nouri",
  "isAnonymous": false,
  "photoURL": "https://...",
  "providerData": [
    {
      "providerId": "google.com",
      "uid": 123,
      "displayName": "Ali Nouri",
      "email": "ali@example.com",
      "phoneNumber": null,
      "photoURL": "https://..."
    }
  ],
  "stsTokenManager": {
    "refreshToken": "...",
    "accessToken": "...",
    "expirationTime": 1234567890
  },
  "createdAt": "1234567890",
  "lastLoginAt": "1234567890",
  "apiKey": "...",
  "appName": "..."
}
```

Success:

```json
{ "token": "<jwt>" }
```

`POST /api/Auth/SignInFacebook`

Anonymous. Same body shape as `SignInGoogle`.

`POST /api/Auth/SignOut`

Authorized. Current implementation returns success and does not revoke the JWT.

`POST /api/Auth/Password`

Anonymous. Body is a raw JSON string, not an object:

```json
"ali@example.com"
```

Success:

```json
{ "message": "لینک بازیابی رمز عبور ارسال شد." }
```

`POST /api/Auth/PasswordFallbackQuestion`

Anonymous.

```json
{ "email": "ali@example.com" }
```

Success:

```json
{
  "questionKey": "pet_name",
  "questionText": "What was your first pet?",
  "hasAnswerConfigured": true
}
```

`POST /api/Auth/PasswordFallback`

Anonymous.

```json
{
  "email": "ali@example.com",
  "questionKey": "pet_name",
  "answer": "Mina",
  "newPassword": "Password2",
  "newPasswordAgain": "Password2"
}
```

`POST /api/Auth/ResetPassword`

Anonymous.

```json
{
  "email": "ali@example.com",
  "token": "<reset-token>",
  "newPassword": "Password2",
  "newPasswordAgain": "Password2"
}
```

### User

All `User` endpoints are authorized.

`GET /api/User/Info`

Returns:

```json
{
  "displayName": "Ali Nouri",
  "userIdentifier": "ali_1989",
  "email": "ali@example.com",
  "phoneNumber": "09123456789",
  "biography": "سلام، من از ChatNest استفاده می‌کنم.",
  "providerId": "email",
  "profilePhoto": "https://...",
  "userSettings": {
    "theme": 0,
    "chatBackground": "color1",
    "showLastSeen": true,
    "showProfilePhoto": true,
    "showBiography": true,
    "notificationEnabled": true,
    "soundEnabled": true,
    "onlineStatus": true,
    "language": "en",
    "readReceiptEnabled": true,
    "lastSeenEnabled": true,
    "profilePhotoVisible": true,
    "groupInviteEnabled": true,
    "securityQuestionKey": "",
    "securityQuestionText": "",
    "securityQuestionAnswerHash": "",
    "securityQuestionAnswerConfigured": false,
    "securityQuestionUpdatedAtUtc": null
  }
}
```

`PATCH /api/User/ProfilePhoto`

```json
{ "profilePhoto": "<base64-image>" }
```

Success includes updated `profilePhoto`. Also broadcasts `ReceiveRecipientProfiles` on the Notification hub.

`DELETE /api/User/ProfilePhoto`

Deletes the current profile photo and broadcasts `ReceiveRecipientProfiles`.

`PATCH /api/User/DisplayName`

```json
{ "displayName": "Ali Nouri" }
```

`PATCH /api/User/UserIdentifier`

```json
{ "userIdentifier": "ali_1989" }
```

`PATCH /api/User/PhoneNumber`

```json
{ "phoneNumber": "09123456789" }
```

`PATCH /api/User/Biography`

```json
{ "biography": "Android developer" }
```

`PATCH /api/User/Password`

```json
{
  "currentPassword": "Password1",
  "newPassword": "Password2",
  "newPasswordAgain": "Password2"
}
```

`PATCH /api/User/SecurityQuestion`

```json
{
  "questionKey": "pet_name",
  "customQuestionText": null,
  "answer": "Mina"
}
```

`PATCH /api/User/Theme`

```json
{ "theme": 2 }
```

`PATCH /api/User/ChatBackground`

```json
{ "chatBackground": "color1" }
```

Accepted values: `color1` through `color9`, or hex colors like `#FF0000`.

### Group

All `Group` endpoints are authorized and use `multipart/form-data`.

`POST /api/Group/Create`

Form fields:

```text
name=My Group
description=Optional description
photo=<base64 or URL depending on UI flow>
photoUrl=https://...
participants=<optional serialized participant data>
selectedParticipants=<repeat field or send indexed list depending on Retrofit multipart support>
kind=0
```

Success:

```json
{ "message": "گروه ایجاد شد." }
```

Side effects:

```text
Creates a group chat.
Sends ReceiveNewGroupProfiles to participants through NotificationHub.
```

`PUT /api/Group/Edit/{groupId}`

Same form fields as create.

Side effect:

```text
Sends ReceiveGroupProfiles to participants through NotificationHub.
```

`DELETE /api/Group/Leave/{groupId}`

Current user leaves the group.

### Chat

All `Chat` endpoints are authorized. These REST endpoints are the preferred Android API for chat list loading, message history, and message mutations. SignalR should still be connected so the client receives the broadcast events caused by these REST actions.

`GET /api/Chat/Initial?skip=0&take=20`

Returns a paged chat list plus profile dictionaries.

```json
{
  "chats": {
    "Individual": {},
    "Group": {}
  },
  "recipientProfiles": {},
  "groupProfiles": {},
  "totalChats": 42,
  "skip": 0,
  "take": 20,
  "hasMore": true
}
```

`take` is clamped server-side to `1..100`.

`GET /api/Chat/Total`

Returns the current user's total chat count as a number.

`GET /api/Chat/{chatId}/Messages?skip=0&take=50`

Returns a paged message response:

```json
{
  "chatId": "guid",
  "chatType": "Individual",
  "totalCount": 100,
  "messages": [],
  "skip": 0,
  "take": 50,
  "nextSkip": 50,
  "hasNextPage": true,
  "isInitial": true
}
```

`GET /api/Chat/{chatId}/MessagesByDay?beforeUtc=2026-06-13T00:00:00Z`

Returns one day of messages before the optional UTC cursor. Use `nextCursorUtc` from the previous response to load older days.

```json
{
  "chatId": "guid",
  "chatType": "Individual",
  "totalCount": 100,
  "messages": [],
  "dayStartUtc": "2026-06-13T00:00:00Z",
  "nextCursorUtc": "2026-06-12T00:00:00Z",
  "hasMore": true,
  "isInitial": true
}
```

`POST /api/Chat/{chatId}/Messages?chatType=Individual`

Sends a text message through REST and broadcasts `ReceiveGetMessages` to participants through `ChatHub`.

```json
{
  "contentType": 0,
  "content": "<encrypted-message-content>",
  "clientMessageId": "android-local-id",
  "replyToMessageId": null,
  "thumbnailUrl": ""
}
```

Success returns the created message envelope.

`POST /api/Chat/{chatId}/Messages/File`

Sends a non-text file message as `multipart/form-data`. Backend max file size is 300 MB.

```text
chatType=Individual
contentType=1
file=<binary file part>
clientMessageId=android-local-id
replyToMessageId=<optional message id>
```

`contentType` must be one of the non-text `MessageContent` enum values.

`DELETE /api/Chat/{chatId}/Messages/{messageId}/ForMe?chatType=Individual`

Deletes a message for the current user and broadcasts the updated message envelope.

`DELETE /api/Chat/{chatId}/Messages/{messageId}/ForEveryone?chatType=Individual`

Deletes a message for everyone. Only the sender can delete for everyone.

`POST /api/Chat/{chatId}/Messages/ForwardAttachment`

Forwards an existing attachment message to an existing chat.

```json
{
  "sourceMessageId": "message-guid",
  "targetChatType": "Individual"
}
```

`POST /api/Chat/Messages/{sourceMessageId}/ForwardToUser/{recipientId}`

Creates or reuses an individual chat with the recipient, forwards the attachment, broadcasts chat/message events, and returns:

```json
{
  "chatId": "target-chat-guid",
  "message": {}
}
```

Retrofit outline:

```kotlin
interface ChatApi {
    @GET("api/Chat/Initial")
    suspend fun initialChats(
        @Query("skip") skip: Int = 0,
        @Query("take") take: Int = 20
    ): ChatInitialResponse

    @GET("api/Chat/{chatId}/Messages")
    suspend fun messages(
        @Path("chatId") chatId: String,
        @Query("skip") skip: Int = 0,
        @Query("take") take: Int = 50
    ): ChatMessagesPageResponse

    @POST("api/Chat/{chatId}/Messages")
    suspend fun sendTextMessage(
        @Path("chatId") chatId: String,
        @Query("chatType") chatType: String,
        @Body body: SendMessageRequest
    ): Map<String, Any>

    @Multipart
    @POST("api/Chat/{chatId}/Messages/File")
    suspend fun sendFileMessage(
        @Path("chatId") chatId: String,
        @Part("chatType") chatType: RequestBody,
        @Part("contentType") contentType: RequestBody,
        @Part file: MultipartBody.Part,
        @Part("clientMessageId") clientMessageId: RequestBody?,
        @Part("replyToMessageId") replyToMessageId: RequestBody?
    ): Map<String, Any>
}
```

### Call REST

All `Call` endpoints are authorized. Use these endpoints for call-log history; keep `CallHub` for realtime call lifecycle and LiveKit token flow.

`GET /api/Call?skip=0&take=20`

Returns paged call logs and recipient profiles.

```json
{
  "calls": {},
  "recipientProfiles": {},
  "totalCalls": 20,
  "skip": 0,
  "take": 20,
  "hasMore": false
}
```

`GET /api/Call/Total`

Returns the current user's total call count as a number.

### Generative AI

All `GenerativeAi` endpoints are authorized.

`POST /api/GenerativeAi/Text`

```json
{
  "aiModel": "gemini",
  "prompt": "Write a short message",
  "model": "optional-provider-model-name"
}
```

Success:

```json
{ "responseText": "..." }
```

`POST /api/GenerativeAi/Image`

```json
{
  "aiModel": "huggingface",
  "prompt": "A chat app mascot",
  "model": "optional-provider-model-name"
}
```

Success:

```json
{ "responseImage": "..." }
```

### Media

`GET /api/Media/{folder}/{publicId}`

Anonymous. Returns binary file content with range processing enabled.

Example:

```text
GET /api/Media/messages/message_2f7f...
```

The backend supports range requests and conditional private caching:

```text
ETag: "<folder-publicId-size-updatedTicks>"
Last-Modified: <http-date>
Cache-Control: private, max-age=31536000, immutable   # message media
Cache-Control: private, max-age=300, must-revalidate  # other media
```

If Android sends `If-None-Match` or `If-Modified-Since` and the file is still fresh, the server returns `304 Not Modified`. Message media under the `messages` folder is treated as immutable for one year. Other folders, such as mutable profile/group media, are cached for five minutes and must be revalidated.

OkHttp honors these headers when an HTTP cache is configured:

```kotlin
val cacheSizeBytes = 100L * 1024L * 1024L
val okHttpClient = OkHttpClient.Builder()
    .cache(Cache(File(context.cacheDir, "chatnest-http"), cacheSizeBytes))
    .addInterceptor(BearerInterceptor(tokenProvider))
    .build()
```

Do not cache authenticated API JSON responses containing private chat/user data in a shared cache. Keep media caching separate from local encrypted message storage.

## Core Response Models

`ChatDto`

```json
{
  "id": "guid",
  "chatType": "Individual",
  "archivedForJson": "{}",
  "createdDate": "2026-06-13T00:00:00Z",
  "messages": [],
  "chatParticipants": []
}
```

`Message`

```json
{
  "id": "guid",
  "content": "text or media URL",
  "thumbnailUrl": "optional thumbnail URL",
  "fileName": "file.pdf",
  "fileSize": 12345,
  "type": 0,
  "senderId": "user-id",
  "chatId": "chat-guid",
  "replyToMessageId": null,
  "replyToSenderId": null,
  "replyToType": null,
  "replyToContent": null,
  "replyToFileName": null,
  "statusJson": "{\"Sent\":{...},\"Delivered\":{},\"Read\":{}}",
  "deletedForJson": "{}",
  "createdDate": "2026-06-13T00:00:00Z",
  "clientMessageId": "android-local-id"
}
```

`RecipientProfile`

```json
{
  "displayName": "Ali Nouri",
  "userIdentifier": "ali_1989",
  "email": "ali@example.com",
  "biography": "Android developer",
  "profilePhoto": "https://...",
  "lastConnectionDate": "2026-06-13T00:00:00Z",
  "isOnline": true
}
```

`GroupProfile`

```json
{
  "id": "group-guid",
  "name": "My Group",
  "description": "Optional",
  "createdDate": "2026-06-13T00:00:00Z",
  "participants": {
    "user-id": {
      "userId": "user-id",
      "displayName": "Ali Nouri",
      "profilePhoto": "https://...",
      "lastConnectionDate": "2026-06-13T00:00:00Z",
      "isOnline": true,
      "role": 0
    }
  },
  "createdBy": "user-id",
  "photoUrl": "https://...",
  "kind": 0
}
```

## SignalR Hubs

Hub URLs:

```text
/hub/Chat
/hub/Call
/hub/Notification
```

Connect with the JWT access-token provider. Register `.on(...)` handlers before calling `start()`.

### Kotlin SignalR Manager Skeleton

```kotlin
class ChatNestRealtime(
    private val baseUrl: String,
    private val tokenProvider: () -> String
) {
    val chatHub = HubConnectionBuilder.create("$baseUrl/hub/Chat")
        .withAccessTokenProvider(Single.defer { Single.just(tokenProvider()) })
        .build()

    val callHub = HubConnectionBuilder.create("$baseUrl/hub/Call")
        .withAccessTokenProvider(Single.defer { Single.just(tokenProvider()) })
        .build()

    val notificationHub = HubConnectionBuilder.create("$baseUrl/hub/Notification")
        .withAccessTokenProvider(Single.defer { Single.just(tokenProvider()) })
        .build()

    fun startAll() {
        notificationHub.start().blockingAwait()
        chatHub.start().blockingAwait()
        callHub.start().blockingAwait()
    }

    fun stopAll() {
        chatHub.stop()
        callHub.stop()
        notificationHub.stop()
    }
}
```

## ChatHub Contract

Client invokes realtime methods on `/hub/Chat`. New Android code should use the REST `Chat` endpoints above for initial chat pages, message history, text/file sends, forwarding, and deletes. Keep the hub connected to receive realtime broadcasts and to send typing/delivery/read events.

### Server Methods

The following read methods still exist for compatibility with older clients. Prefer REST in new code:

`Initial(skip: Int = 0, take: Int = 5)`

Loads chat list, group profiles, and recipient profiles.

Emits:

```text
ReceiveInitialChats(Dictionary<String, Dictionary<String, ChatDto>>)
ReceiveInitialGroupProfiles(Dictionary<String, GroupProfile>)
ReceiveInitialRecipientChatProfiles(Dictionary<String, RecipientProfile>)
```

`TotalChatList()`

Emits:

```text
ReceiveTotalChats(Int)
```

`GetTotalMessageCountAsync(chatId: String)`

Emits:

```text
ReceiveTotalChatMessages(Int)
```

`GetChatMessagesAsync(chatId: String, skip: Int = 0, take: Int = 5)`

Emits:

```text
ReceiveChatMessages({
  totalCount: Int,
  messages: [Message]
})
```

`GetChatMessagesByDayAsync(chatId: String, beforeUtc: String? = null)`

Legacy hub equivalent for day-based pagination. Prefer `GET /api/Chat/{chatId}/MessagesByDay?beforeUtc=` in new Android code.

Emits:

```json
{
  "chatId": "guid",
  "chatType": "Individual",
  "totalCount": 100,
  "messages": [],
  "dayStartUtc": "2026-06-13T00:00:00Z",
  "nextCursorUtc": "2026-06-12T00:00:00Z",
  "hasMore": true,
  "isInitial": true
}
```

Realtime chat actions:

`CreateChat(chatType: String, recipientId: String)`

`chatType` is `Individual` or `Group`.

For individual chats, `recipientId` is a user id. For group chats, `recipientId` is the group id.

Emits to participants:

```text
ReceiveCreateChat(Dictionary<String, Dictionary<String, ChatDto>>)
ReceiveRecipientProfiles(Dictionary<String, Dictionary<String, Any>>) for individual profile updates
```

`GetOrCreateSavedMessagesChat(): String`

Creates or returns a self-chat. Also broadcasts chat/profile updates to the caller.

`ClearChat(chatType: String, chatId: String)`

Marks messages deleted for current user and emits:

```text
ReceiveClearChat(Dictionary<String, Dictionary<String, ChatDto>>)
```

`ArchiveChat(chatId: String)`

Individual chats only. Emits:

```text
ReceiveArchiveChat(Dictionary<String, Dictionary<String, Dictionary<String, DateTime>>>)
```

`UnarchiveChat(chatId: String)`

Emits:

```text
ReceiveUnarchiveChat(Dictionary<String, Dictionary<String, Dictionary<String, DateTime>>>)
```

`SendMessage(chatType: String, chatId: String, dto: SendMessage)`

Legacy hub send method. Prefer `POST /api/Chat/{chatId}/Messages?chatType=` in new Android code so Android matches the current web UI service flow.

Text example:

```json
{
  "contentType": 0,
  "content": "Hello",
  "clientMessageId": "local-uuid",
  "replyToMessageId": null,
  "thumbnailUrl": ""
}
```

For non-text messages, `content` may be base64 file data, or use the chunk upload flow below.

Emits to participants:

```text
ReceiveGetMessages(Dictionary<String, Dictionary<String, Dictionary<String, Message>>>)
```

`BeginFileUpload(chatType: String, chatId: String, contentType: Int, fileName: String, clientMessageId: String?, replyToMessageId: String?): String`

Legacy chunked upload flow. Prefer `POST /api/Chat/{chatId}/Messages/File` with `multipart/form-data` in new Android code. `contentType` must not be `Text`. Backend max file size is 300 MB.

`UploadFileChunk(uploadId: String, base64Chunk: String)`

Appends one base64 chunk to the pending upload.

`CompleteFileUpload(uploadId: String)`

Stores the uploaded file, creates the message, and emits:

```text
ReceiveGetMessages(...)
```

`AbortFileUpload(uploadId: String)`

Cancels a pending upload. No event is emitted.

`ForwardAttachment(targetChatType: String, targetChatId: String, sourceMessageId: String)`

Legacy hub attachment forward. Prefer `POST /api/Chat/{chatId}/Messages/ForwardAttachment`.

`ForwardAttachmentToUser(recipientId: String, sourceMessageId: String)`

Legacy hub attachment forward. Prefer `POST /api/Chat/Messages/{sourceMessageId}/ForwardToUser/{recipientId}`.

`DeliverMessage(chatType: String, chatId: String, messageId: String)`

Marks the message delivered by current user and emits updated message data.

`ReadMessage(chatType: String, chatId: String, messageId: String)`

Marks the message read by current user and emits updated message data.

`DeleteMessage(chatType: String, chatId: String, messageId: String, deletionType: Byte)`

Legacy hub delete. Prefer the REST `ForMe` and `ForEveryone` delete endpoints. `deletionType = 1` deletes for everyone and only sender can do it. Other values delete for current user only.

`JoinGroup(groupId: String)`

Adds this SignalR connection to `Group_{groupId}` and emits:

```text
JoinedGroup({ groupId })
```

`LeaveGroup(groupId: String)`

Emits:

```text
LeftGroup({ groupId })
```

`UpdateOnlineStatus(isOnline: Boolean)`

The server ignores the requested boolean and uses tracked connection state. It broadcasts:

```text
ReceiveRecipientProfiles({ "<userId>": { "isOnline": true/false, "lastConnectionDate": "..." } })
```

`UpdateTypingStatus(chatId: String, isTyping: Boolean)`

Emits to other chat participants:

```text
UserTyping({ chatId, userId, isTyping })
```

### ChatHub Client Events

Register handlers for:

```text
ConnectionError
UnexpectedError
ValidationError
ReceiveInitialChats
ReceiveInitialGroupProfiles
ReceiveInitialRecipientChatProfiles
ReceiveTotalChats
ReceiveTotalChatMessages
ReceiveChatMessages
ReceiveCreateChat
ReceiveRecipientProfiles
ReceiveClearChat
ReceiveArchiveChat
ReceiveUnarchiveChat
ReceiveGetMessages
JoinedGroup
LeftGroup
UserTyping
```

### Kotlin ChatHub Examples

```kotlin
chatHub.on("ValidationError", { err: Map<String, Any> ->
    // err["message"]
}, object : TypeReference<Map<String, Any>>() {}.type)

chatHub.on("ReceiveGetMessages", { data: Map<String, Any> ->
    // data["Individual"] or data["Group"]
}, object : TypeReference<Map<String, Any>>() {}.type)

chatHub.send("CreateChat", "Individual", recipientUserId)

// Load pages and send messages through REST:
chatApi.initialChats(skip = 0, take = 20)
chatApi.messages(chatId, skip = 0, take = 50)
chatApi.sendTextMessage(
    chatId,
    "Individual",
    SendMessageRequest(
        contentType = 0,
        content = encryptedMessage,
        clientMessageId = UUID.randomUUID().toString(),
        replyToMessageId = null,
        thumbnailUrl = ""
    )
)
```

Legacy chunked upload:

```kotlin
val uploadId = chatHub
    .invoke(
        String::class.java,
        "BeginFileUpload",
        "Individual",
        chatId,
        1,
        "photo.jpg",
        UUID.randomUUID().toString(),
        null
    )
    .blockingGet()

fileBytes.asIterable()
    .chunked(64 * 1024)
    .forEach { chunk ->
        val base64 = Base64.encodeToString(chunk.toByteArray(), Base64.NO_WRAP)
        chatHub.send("UploadFileChunk", uploadId, base64)
    }

chatHub.send("CompleteFileUpload", uploadId)
```

## NotificationHub Contract

Client invokes server methods on `/hub/Notification`.

### Server Methods

`Initial()`

Emits:

```text
NotificationHubInitialized({ status: "connected", timestamp: DateTime })
```

`SearchUsers(query: String)`

Emits:

```json
{
  "query": "ali",
  "data": {
    "user-id": {
      "displayName": "Ali Nouri",
      "userIdentifier": "ali_1989",
      "email": "ali@example.com",
      "profilePhoto": "https://..."
    }
  }
}
```

Connection side effects:

```text
On connect: ReceiveRecipientProfiles({ "<userId>": { "isOnline": true } }) to others
On final disconnect: ReceiveRecipientProfiles({ "<userId>": { "isOnline": false, "lastConnectionDate": "..." } }) to others
```

REST `User` and `Group` changes also broadcast profile events through NotificationHub:

```text
ReceiveRecipientProfiles
ReceiveNewGroupProfiles
ReceiveGroupProfiles
```

### NotificationHub Client Events

```text
ConnectionError
UnexpectedError
ValidationError
NotificationHubInitialized
ReceiveSearchUsers
ReceiveRecipientProfiles
ReceiveNewGroupProfiles
ReceiveGroupProfiles
```

## CallHub Contract

Client invokes server methods on `/hub/Call`.

SignalR manages call lifecycle and signaling. LiveKit media sessions are joined with a token returned by `CreateLiveKitJoinToken`.

### Server Methods

`Initial()`

Legacy hub call-log load. Prefer `GET /api/Call?skip=&take=` for new Android code.

Emits:

```text
ReceiveInitialCalls({ calls: { "<callId>": Call } })
ReceiveInitialCallRecipientProfiles(Dictionary<String, CallerUser>)
```

`StartCall(recipientId: String, callType: Int)`

`callType`: `0=Voice`, `1=Video`.

Emits to caller:

```json
{
  "callId": "guid",
  "callType": 1,
  "callerId": "caller-user-id",
  "recipientId": "recipient-user-id",
  "profileUserId": "recipient-user-id",
  "profile": {}
}
```

Event name:

```text
ReceiveOutgoingCall
```

Emits to recipient:

```text
ReceiveIncomingCall
```

`AcceptCall(callId: String)`

Emits to accepting user:

```text
ReceiveAcceptCall(true)
```

`CreateLiveKitJoinToken(callId: String, displayName: String?): Object`

Returns directly from SignalR invocation:

```json
{
  "serverUrl": "wss://livekit.example.com",
  "token": "<livekit-jwt>",
  "roomName": "chatnest-call-<call-guid-without-dashes>",
  "identity": "chatnest-<userId>",
  "expiresAt": "2026-06-13T00:30:00Z",
  "callId": "guid",
  "callType": 1
}
```

Use `serverUrl` and `token` with the LiveKit Android SDK. In production, LiveKit recommends generating tokens on the server rather than hardcoding them in clients.

`SendSdp(callId: String, sdp: JsonElement)`

Relays WebRTC SDP to other participants.

Emits:

```text
ReceiveSdp({ callId, sdp, callType, senderId })
```

If you use LiveKit for media, you usually do not need custom SDP/ICE handling in the Android client.

`SendIceCandidate(callId: String, iceCandidate: JsonElement)`

Relays candidate to other participants.

Emits:

```text
ReceiveIceCandidate({ callId, iceCandidate, senderId })
```

`EndCall(callId: String, callStatus: Int, createdDate: DateTime?)`

`callStatus`: use `Accepted`, `Declined`, `Canceled`, `Missed`, etc. The backend sets call duration when `createdDate` is provided.

Emits to participants:

```text
ReceiveEndCall({
  "call": { "<callId>": Call },
  "<otherUserId>": CallerUser
})
```

`DeleteCall(callId: String)`

Marks the call deleted for current user.

Emits:

```text
ReceiveDeleteCall(callId)
```

Disconnect behavior:

```text
On disconnect, ongoing calls for the user are ended and ReceiveEndCall can be emitted to participants.
```

### CallHub Client Events

```text
ConnectionError
UnexpectedError
ValidationError
ReceiveInitialCalls
ReceiveInitialCallRecipientProfiles
ReceiveOutgoingCall
ReceiveIncomingCall
ReceiveAcceptCall
ReceiveSdp
ReceiveIceCandidate
ReceiveEndCall
ReceiveDeleteCall
```

### Kotlin CallHub Examples

```kotlin
callHub.on("ReceiveIncomingCall", { data: Map<String, Any> ->
    val callId = data["callId"] as String
    // Show incoming call UI.
}, object : TypeReference<Map<String, Any>>() {}.type)

callHub.send("StartCall", recipientUserId, 1)
callHub.send("AcceptCall", callId)

val liveKitToken = callHub.invoke(
    object : TypeReference<Map<String, Any>>() {}.type,
    "CreateLiveKitJoinToken",
    callId,
    displayName
).blockingGet()

val serverUrl = liveKitToken["serverUrl"] as String
val token = liveKitToken["token"] as String
```

LiveKit connection outline:

```kotlin
val room = LiveKit.create(context)
room.connect(serverUrl, token)
room.localParticipant.setMicrophoneEnabled(true)
room.localParticipant.setCameraEnabled(callType == 1)
```

## Retrofit Interfaces

```kotlin
interface AuthApi {
    @POST("api/Auth/SignInEmail")
    suspend fun signInEmail(@Body body: SignInEmailRequest): TokenResponse

    @POST("api/Auth/SignUp")
    suspend fun signUp(@Body body: SignUpRequest): MessageResponse
}

interface UserApi {
    @GET("api/User/Info")
    suspend fun info(): UserInfoDto

    @PATCH("api/User/DisplayName")
    suspend fun displayName(@Body body: UpdateDisplayNameRequest): MessageResponse

    @PATCH("api/User/ProfilePhoto")
    suspend fun profilePhoto(@Body body: UpdateProfilePhotoRequest): Map<String, Any>
}

interface GroupApi {
    @Multipart
    @POST("api/Group/Create")
    suspend fun createGroup(
        @Part("name") name: RequestBody,
        @Part("description") description: RequestBody?,
        @Part("photo") photo: RequestBody?,
        @Part("photoUrl") photoUrl: RequestBody?,
        @Part("participants") participants: RequestBody?,
        @Part("kind") kind: RequestBody
    ): MessageResponse
}
```

OkHttp bearer interceptor:

```kotlin
class BearerInterceptor(private val tokenProvider: () -> String?) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val token = tokenProvider()
        val request = chain.request().newBuilder().apply {
            if (!token.isNullOrBlank()) {
                header("Authorization", "Bearer $token")
            }
        }.build()

        return chain.proceed(request)
    }
}
```

## Android Implementation Notes

1. Connect to `NotificationHub` first so presence updates are tracked early.
2. Register all SignalR handlers before calling `start()`.
3. Keep the JWT in encrypted storage and refresh/re-login before reconnecting after token expiry.
4. Load chats, message history, and call logs through REST, not SignalR.
5. Send text messages, file messages, attachment forwards, and deletes through REST; keep listening for `ReceiveGetMessages` to reconcile realtime state.
6. Treat all nested SignalR dictionaries as maps keyed by ids: chat type, chat id, message id, user id.
7. Prefer `clientMessageId` for optimistic UI reconciliation after `ReceiveGetMessages`.
8. Use multipart REST file upload for new Android code. Use hub chunked upload only for legacy clients that cannot send multipart files.
9. Configure an OkHttp media cache so `/api/Media/...` can reuse `ETag`, `Last-Modified`, range requests, and `304 Not Modified`.
10. The web UI service worker cache version is web-only. Android should use its own HTTP/media cache and must not cache authenticated chat JSON in a shared browser-style app shell cache.
11. For channel groups (`GroupKind.Channel`), only admins can send messages.
12. For self-chat, call `GetOrCreateSavedMessagesChat()`.
13. Call `UpdateTypingStatus(chatId, true/false)` with debouncing.
14. Use `ReadMessage` when a message becomes visible and `DeliverMessage` when it arrives locally.
