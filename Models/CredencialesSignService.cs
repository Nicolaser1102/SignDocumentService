namespace Models;

public class GenericResponse<T>
{
    public int CodeReturn { get; set; }
    public string? Message { get; set; }
    public T? Result { get; set; }
}


public class LoginRequestGS
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public int SessionId { get; set; }
    public string? Token { get; set; }
    public string? Result { get; set; }
}


public class ExternalUrls
{
    public string LoginUrl { get; set; }
    public string ObtenerSmsUrl { get; set; }
    public string ActualizarSmsUrl { get; set; }
    public string AuthSmsUrl { get; set; }
    public string GenericExecuteUrl { get; set; }
    public string LoginUser { get; set; }
    public string LoginPassword { get; set; }
    public string NameService { get; set; }
    public string HashPassword { get; set; }
    public string NamePC { get; set; }
    public string LoginAPP { get; set; }
}

public class GenericRequestIntegracion
{
    public string Action { get; set; } = string.Empty;
    public object? Data { get; set; }


}

