namespace taskFlow.Models
{
    public class Response<T>
    {
        public string? Message { get; set; }
        public bool Status { get; set; }
        public T? Data { get; set; }

        public Response()
        {
        }

        public Response(bool status, string? message, T? data)
        {
            Status = status;
            Message = message;
            Data = data;
        }

        public static Response<T> Success(T? data, string message = "Operation successful")
        {
            return new Response<T>(true, message, data);
        }

        public static Response<T> Failure(string message = "Operation failed")
        {
            return new Response<T>(false, message, default);
        }
    }
}
