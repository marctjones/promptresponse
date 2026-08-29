package org.promptresponse.demo;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import org.promptresponse.*;

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/** A deliberately minimal, no-framework local form demo. It never contacts a network service. */
public final class AprDemoServer {
    private final AprDocument document;
    private final Path output;
    private AprDemoServer(Path input, Path output) throws IOException { this.document=Apr.read(input); this.output=output; }
    public static void main(String[] args) throws Exception {
        if(args.length < 1 || args.length > 2) { System.err.println("Usage: AprDemoServer FORM.aprt [completed.aprf]"); System.exit(2); }
        Path source=Path.of(args[0]); Path output=args.length==2 ? Path.of(args[1]) : source.resolveSibling(source.getFileName().toString().replaceAll("\\.apr[tf]$", "")+"-completed.aprf");
        AprDemoServer app=new AprDemoServer(source,output); HttpServer server=HttpServer.create(new InetSocketAddress("127.0.0.1",8082),0);
        server.createContext("/", app::home); server.createContext("/submit", app::submit); server.start();
        System.out.println("APR Java demo at http://127.0.0.1:8082/ — output: "+output.toAbsolutePath());
    }
    private void home(HttpExchange exchange) throws IOException {
        if(!"GET".equals(exchange.getRequestMethod())) { respond(exchange,405,"Method not allowed","text/plain"); return; }
        ValidationResult result=Apr.validate(document); StringBuilder html=new StringBuilder("<!doctype html><meta charset=utf-8><title>").append(esc(title())).append("</title><main><h1>").append(esc(title())).append("</h1>");
        if(!result.isValid()) { html.append("<aside role=alert><h2>Document problems</h2><ul>"); for(ValidationIssue error:result.errors()) html.append("<li>").append(esc(error.path()+": "+error.message())).append("</li>"); html.append("</ul></aside>"); }
        html.append("<form method=post action=/submit>"); fields(document.sections(),html); html.append("<button type=submit>Save completed APRF</button></form></main>"); respond(exchange,200,html.toString(),"text/html; charset=utf-8");
    }
    @SuppressWarnings("unchecked") private static void fields(List<Object> sections,StringBuilder html) {
        for(Object item:sections) { Map<String,Object> section=(Map<String,Object>)item; html.append("<fieldset><legend>").append(esc(string(section.get("title")))).append("</legend>"); String description=string(section.get("description")); if(description!=null) html.append("<p>").append(esc(description)).append("</p>");
            for(Object promptItem:(List<Object>)section.getOrDefault("prompts",List.of())) { Map<String,Object> prompt=(Map<String,Object>)promptItem; String id=string(prompt.get("id")); html.append("<label for=").append(attr(id)).append("> ").append(esc(string(prompt.get("label")))).append("</label><input id=").append(attr(id)).append(" name=").append(attr(id)).append(" value=").append(attr(string(prompt.get("response")))).append("><br>"); }
            fields((List<Object>)section.getOrDefault("sections",List.of()),html); html.append("</fieldset>"); }
    }
    private void submit(HttpExchange exchange) throws IOException {
        if(!"POST".equals(exchange.getRequestMethod())) { respond(exchange,405,"Method not allowed","text/plain"); return; }
        String body=new String(exchange.getRequestBody().readAllBytes(),StandardCharsets.UTF_8);
        for(String pair:body.split("&")) { int equals=pair.indexOf('='); if(equals < 0) continue; String id=URLDecoder.decode(pair.substring(0,equals),StandardCharsets.UTF_8); String value=URLDecoder.decode(pair.substring(equals+1),StandardCharsets.UTF_8); try { document.setResponse(id,value); } catch(IllegalArgumentException ignored) { /* Only known APR prompt ids can be changed. */ } }
        AprExpressions.recomputeComputedValues(document);
        Apr.write(document,output); respond(exchange,200,"<!doctype html><meta charset=utf-8><p>Saved <code>"+esc(output.toAbsolutePath().toString())+"</code>.</p><p><a href=/>Return to form</a></p>","text/html; charset=utf-8");
    }
    private String title() { return string(document.metadata().get("title")); }
    private static String string(Object value) { return value instanceof String text ? text : ""; }
    private static String esc(String text) { return text.replace("&","&amp;").replace("<","&lt;").replace(">","&gt;"); }
    private static String attr(String text) { return esc(text).replace("\"","&quot;"); }
    private static void respond(HttpExchange exchange,int status,String body,String type) throws IOException { byte[] bytes=body.getBytes(StandardCharsets.UTF_8); exchange.getResponseHeaders().set("Content-Type",type); exchange.sendResponseHeaders(status,bytes.length); try(OutputStream output=exchange.getResponseBody()){output.write(bytes);} }
}
