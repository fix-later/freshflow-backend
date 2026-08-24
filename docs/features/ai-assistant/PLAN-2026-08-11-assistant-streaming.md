# Plan: Streaming cho AI Assistant

**Ngày:** 2026-08-11  
**Trạng thái:** Ý tưởng để triển khai sau  
**Phạm vi:** Stream câu trả lời Gemini/ZenMux, giữ nguyên tool calling, confirmation gate và conversation store.

## 1. Kết luận kỹ thuật

Connector hiện tại đã hỗ trợ streaming:

- `Microsoft.Extensions.AI.OpenAI 10.7.0`: `IChatClient.GetStreamingResponseAsync(...)`.
- `OpenAI 2.11.0`: gọi Chat Completions dạng stream.
- Vertex AI OpenAI-compatible endpoint hỗ trợ cả `stream` và `tools`.

Không cần thay connector hoặc thêm SDK. Việc cần làm nằm ở wrapper, orchestrator, thought-signature handler và HTTP transport.

## 2. Transport đề xuất

Thêm endpoint:

```http
POST /api/v1/assistant/chat/stream
Accept: text/event-stream
Content-Type: application/json
```

Dùng SSE thay vì tạo WebSocket/SignalR Hub mới vì mỗi request chỉ cần stream một chiều từ server về client. Giữ nguyên endpoint non-streaming hiện tại để tương thích client cũ.

Chỉ chuyển sang SignalR khi cần nhiều tính năng hai chiều trên cùng kết nối, ví dụ hủy generation bằng message riêng, theo dõi nhiều phiên đồng thời hoặc resume stream sau reconnect.

## 3. Event contract tối thiểu

```text
event: delta
data: {"text":"Đơn hàng"}

event: confirmation_required
data: {"orderId":"...","deliveryAddressId":"...","preview":{...}}

event: done
data: {"sessionId":"...","draftOrderId":"..."}

event: error
data: {"code":"ASSISTANT_PROVIDER_UNAVAILABLE","message":"..."}
```

Không gửi tool arguments, tool result hoặc thought signature ra frontend. Nếu cần UX tốt hơn có thể thêm `tool_started` sau, nhưng không bắt buộc cho phiên bản đầu.

## 4. Các thay đổi backend

### 4.1. Làm thought-signature handler tương thích SSE

`GeminiThoughtSignatureHandler` hiện đọc toàn bộ response bằng `ReadAsByteArrayAsync` trước khi trả response cho connector. Với `text/event-stream`, việc này làm mất streaming và không thể parse toàn bộ SSE như một JSON object.

Thay đổi tối thiểu:

1. Giữ nguyên logic inject signature vào request.
2. Chỉ chạy `CaptureSignaturesAsync` với response JSON non-streaming.
3. Nếu response là `text/event-stream`, trả response ngay, không đọc body.
4. Ở tool hop kế tiếp, dùng fallback `skip_thought_signature_validator` hiện có khi connector không giữ được signature từ SSE.

Đây là phương án đầu tiên nên triển khai. Google cho phép dummy signature để bỏ validation. Chỉ viết parser SSE để giữ signature thật nếu smoke test cho thấy chất lượng multi-step tool calling giảm rõ rệt.

### 4.2. Bổ sung streaming vào chat client

Thêm một method streaming vào `IAssistantChatClient`. Implementation cần:

1. Tạo messages và `ChatOptions` giống `CompleteAsync`.
2. Đọc `_inner.GetStreamingResponseAsync(...)`.
3. Chuyển từng text delta cho caller ngay khi nhận được.
4. Giữ các `ChatResponseUpdate` và dùng helper `ToChatResponse()` có sẵn của Microsoft.Extensions.AI để ghép tool-call arguments.
5. Tái sử dụng mapping hiện tại để trả `AssistantTurnResult` cuối cùng.

Không tự viết parser tool-call delta vì connector đã thực hiện phần ghép này.

Áp dụng cho cả Gemini và ZenMux. `CompleteAsync` cũ vẫn giữ lại cho endpoint cũ.

### 4.3. Failover khi đang stream

Quy tắc:

- Provider lỗi trước khi phát delta đầu tiên: được phép chuyển sang provider tiếp theo.
- Provider lỗi sau khi đã phát delta: không failover vì có thể tạo câu trả lời trùng hoặc lệch; phát event `error` và kết thúc stream.

### 4.4. Bổ sung streaming orchestration

Thêm luồng streaming song song với `RunAsync`, nhưng giữ nguyên các quy tắc hiện tại:

- Tối đa `MaxToolHops`.
- Tool call vẫn qua `ConfirmationGate`.
- `confirm_order` không được chạy nếu thiếu cờ xác nhận từ client.
- Tool result chỉ đưa lại cho LLM, không gửi ra frontend.
- Conversation chỉ được lưu sau khi có kết quả cuối hoặc pending confirmation hợp lệ.
- Nếu client ngắt kết nối, truyền cancellation token xuống provider và không lưu câu trả lời dang dở.

### 4.5. Endpoint SSE

Thêm `POST /api/v1/assistant/chat/stream` vào `AssistantController` hoặc một controller Assistant riêng nếu file hiện tại trở nên khó đọc.

Trước khi bắt đầu response stream phải hoàn thành:

- Request validation.
- JWT/user ownership check.
- Load conversation/session.

Sau khi response đã bắt đầu, không thể đổi HTTP status. Lỗi phát sinh lúc đó phải gửi bằng event `error`.

Tái sử dụng rate-limit policy `assistant`; không tạo policy mới.

## 5. Thứ tự triển khai

1. Sửa `GeminiThoughtSignatureHandler` để không buffer SSE.
2. Thêm streaming method cho Gemini/ZenMux và test text/tool-call aggregation.
3. Thêm failover rule trước/sau delta đầu tiên.
4. Thêm streaming orchestrator, giữ nguyên gate và tool-hop loop.
5. Thêm endpoint SSE và event contract.
6. Thêm frontend reader bằng `fetch()` + `ReadableStream` vì request là `POST`; không dùng browser `EventSource` thuần.
7. Smoke test trên server với Gemini thật.

## 6. Kiểm thử bắt buộc

### Unit

- Text response được phát thành nhiều `delta` theo đúng thứ tự.
- Tool-call arguments bị chia nhiều chunk vẫn ghép thành một `AssistantTurnResult` đúng.
- SSE response không bị `GeminiThoughtSignatureHandler` đọc trước.
- Request tool hop sau có thought signature hoặc dummy signature hợp lệ.
- Failover chỉ xảy ra trước delta đầu tiên.
- Confirmation gate vẫn chặn `confirm_order` khi thiếu cờ xác nhận.

### Integration

- Endpoint yêu cầu role `restaurant` và dùng rate limit hiện tại.
- Event kết thúc theo thứ tự `delta* -> done`.
- Tool flow theo thứ tự `tool call -> tool result -> delta* -> done`.
- Pending confirmation trả `confirmation_required -> done` và không confirm order.
- Client disconnect hủy provider request và không lưu assistant turn dang dở.

### Smoke test Gemini thật

1. Hỏi câu chỉ cần text và kiểm tra chunk xuất hiện dần.
2. Hỏi tìm sản phẩm để tạo ít nhất một tool hop.
3. Thử chuỗi tool nhiều bước và kiểm tra không còn lỗi 400 `missing thought_signature`.
4. Tạm gây 429 ở primary để kiểm tra failover trước delta đầu tiên.

## 7. Tiêu chí hoàn thành

- Client thấy delta đầu tiên trước khi Gemini hoàn thành toàn bộ câu trả lời.
- Tool calling và confirmation hoạt động như endpoint cũ.
- Không xuất hiện lỗi 400 thought signature trong smoke test nhiều bước.
- Session chỉ chứa câu trả lời hoàn chỉnh.
- Endpoint non-streaming cũ không đổi behavior.

## 8. Không làm trong phiên bản đầu

- Không thêm WebSocket Hub mới.
- Không stream nội dung tool hoặc reasoning/thought summary.
- Không resume stream sau reconnect.
- Không lưu partial response.
- Không viết parser SSE thought signature riêng trừ khi dummy signature gây vấn đề đo được.

## Tài liệu tham khảo

- [Microsoft.Extensions.AI OpenAI README](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI)
- [Vertex AI OpenAI-compatible Chat Completions](https://cloud.google.com/vertex-ai/generative-ai/docs/multimodal/call-vertex-using-openai-library)
- [Gemini thought signatures](https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures)
